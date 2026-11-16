using System.Diagnostics;
using System.Drawing;
using Pong;
using Pong.AI;
using Pong.AI.FrameImageToXY;
using Pong.GameArea;

string mode = args.Length > 0 ? args[0] : "all";

if (mode is "vision" or "all") CheckVision();

if (mode is "train" or "aim" or "all")
{
    NeuralNetwork net = TrainOrLoad();
    if (mode is "aim" or "all") AimExperiment(net);
}

/// <summary>Does what the eyes report match what the simulator assumes? (Uses the game's real draw/crop/extract path.)</summary>
static void CheckVision()
{
    var brain = new ImageFrameToXYBrain();
    var rnd = new Random(1);
    double worst = 0; int fails = 0, n = 5000;

    for (int k = 0; k < n; k++)
    {
        var ball = new Ball(Vision.CourtWidth, Vision.CourtHeight, BallDirection.LeftToRight);
        ball.X = (float)(Vision.WindowOriginX + 3 + rnd.NextDouble() * (Vision.WidthOfEyes - 6));
        ball.Y = (float)(3 + rnd.NextDouble() * (Vision.CourtHeight - 6));

        using var court = new Bitmap(Vision.CourtWidth, Vision.CourtHeight);
        using (var g = Graphics.FromImage(court)) { g.Clear(Color.Black); ball.Draw(g); g.Flush(); }

        using var eyes = new Bitmap((int)Vision.WidthOfEyes, Vision.CourtHeight);
        using (var g = Graphics.FromImage(eyes)) g.DrawImageUnscaled(court, -Vision.WindowOriginX, 0, (int)Vision.WidthOfEyes, eyes.Height);

        var xy = brain.AIMapImageIntoXY(FormPong.AIviewFromAboveAndDecideResponse30x262pixels(eyes));
        double ex = Math.Round(ball.X - 2) - Vision.WindowOriginX + 2, ey = Math.Round(ball.Y - 2) + 2; // what SyntheticData assumes
        if (xy is null) { fails++; continue; }
        worst = Math.Max(worst, Math.Max(Math.Abs(xy[0] - ex), Math.Abs(xy[1] - ey)));
    }

    Console.WriteLine($"vision vs simulator: worst difference {worst}px over {n} balls, ball not found {fails}");
}

/// <summary>Loads the game's cached weights if present (same shape), otherwise trains from simulated data.</summary>
static NeuralNetwork TrainOrLoad()
{
    NeuralNetwork net = new(new[] { BallPredictor.FeatureCount, 32, 16, 1 });
    string cache = $@"c:\temp\pong-4.6-{BallPredictor.FeatureCount}-32-16-1.ai";

    if (net.Load(cache))
    {
        Console.WriteLine($"using game's cached weights {cache}");
    }
    else
    {
        Stopwatch sw = Stopwatch.StartNew();
        Pretrainer.Train(net, 100_000, 5_000, 40, (e, mae, hit) => { if (e % 10 == 0) Console.WriteLine($"  epoch {e}: test error {mae:F2}px, in reach {hit:P1}"); });
        Console.WriteLine($"trained in {sw.Elapsed.TotalSeconds:F0}s");
    }

    var test = SyntheticData.Generate(10_000);
    var (mae2, hit2) = Pretrainer.Evaluate(net, test);
    var (maeB, hitB) = Pretrainer.EvaluateBaseline(test);
    Console.WriteLine($"held-out prediction: network {mae2:F2}px / reach {hit2:P1};  straight-line baseline {maeB:F1}px / reach {hitB:P1}");
    return net;
}

/// <summary>
/// Return a ball centred (offset 0) vs aimed (ReturnPolicy), using the network's REAL prediction errors, and measure
/// both the price (missed balls) and the prize (returns that land far from where the opponent stands).
/// </summary>
static void AimExperiment(NeuralNetwork net)
{
    var items = SyntheticData.Generate(20_000);

    double netErr = items.Average(t => Math.Abs(BallPredictor.NNToY(net.FeedForward(t.GetTrainingData())[0]) - t.YPositionOfTheBallWhenItArrivesOnLeftBatLine));
    Console.WriteLine($"\nnetwork mean error on this set: {netErr:F2}px  (policy would cap the offset at {ReturnPolicy.MaxOffsetFor(netErr)}px for this)");
    Console.WriteLine("\nmax offset | bat hits ball | mean |zone| | mean return travel | return unreachable (point won) | mean offset used");

    foreach (int maxOffset in new[] { 0, 6, 8, 10, 12, 14 })
    {
        int hits = 0, won = 0, zoneCount = 0; double travel = 0, zoneAbs = 0, offsetSum = 0;

        foreach (var t in items)
        {
            double pred = BallPredictor.NNToY(net.FeedForward(t.GetTrainingData())[0]);
            ReturnPolicy.Aim aim = ReturnPolicy.Choose(pred, t.frame2X, t.frame3X, maxOffset);

            int truth = (int)Math.Round(t.YPositionOfTheBallWhenItArrivesOnLeftBatLine);
            int zone = Bat.HitZone(truth - aim.BatY);
            if (zone == int.MaxValue) continue; // missed: opponent scores

            hits++;
            float dx = (float)Math.Round((t.frame3X - t.frame2X) / (Vision.Frame3 - Vision.Frame2), 1);
            double landing = ReturnPolicy.SimulateReturn(truth, dx, zone);
            travel += Math.Abs(landing - truth);
            if (ReturnPolicy.Unreachable(landing)) won++;
            zoneAbs += Math.Abs(zone); zoneCount++;
            offsetSum += Math.Abs(truth - aim.BatY);
        }

        Console.WriteLine($"{maxOffset,10} | {hits * 100.0 / items.Count,12:F1}% | {zoneAbs / zoneCount,11:F2} | {travel / hits,17:F1}px | {won * 100.0 / items.Count,29:F2}% | {offsetSum / hits,6:F1}px");
    }
}
