using Pong.AI;
using Pong.AI.FrameImageToXY;
using Pong.GameArea;
using Pong.Utils;
using System.Diagnostics;
using System.Security.Cryptography;

namespace Pong;

/// <summary>
/// Pong form, using images to move the bat.
/// </summary>
public partial class FormPong : Form
{
    /// <summary>
    /// A prediction within this many pixels of where the ball really arrived is shown as "accurate" in the visualiser.
    /// (The bat is 32px long, so a few pixels is comfortably a hit.)
    /// </summary>
    internal const float c_maxAllowedDiffInAIVsExpected = 4f;

    /// <summary>
    /// Trained weights are cached here so the simulated pre-training only happens once. The name includes the network
    /// shape, so changing the layers or features does not load incompatible weights.
    /// </summary>
    private static string WeightsFile => $@"c:\temp\pong-6-{BallPredictor.FeatureCount}-32-16-1.ai";

    /// <summary>How many simulated trajectories / passes the first-run pre-training uses.</summary>
    private const int c_pretrainSamples = 100000;
    private const int c_pretrainEpochs = 40;

    /// <summary>
    /// true - it will draw lines showing the "dead" zone and the "goal" line.
    /// </summary>
    private const bool c_drawDebugLines = false;

    /// <summary>
    /// We could snapshot the whole tennis court, and use that, but AI would need to learn to ignore the numbers and bats.
    /// So we are selective in when we snapshot (before numbers), and what area (excludes bat).
    /// This is used for both images (ball-hits-bat + ball "n" frames after hitting bat).
    /// </summary>
    private static Bitmap snapShotImageTakenOfTheTennisCourtContainingTheBall;

    /// <summary>
    /// Training generation.
    /// </summary>
    private int epoch = 0;

    /// <summary>
    /// >0 indicates how many times the ball has been returned. Each round it speeds up.
    /// </summary>
    private int round = 0;

    /// <summary>
    /// Left "bat".
    /// </summary>
    private readonly Bat batOnTheLeftControlledByAI;

    /// <summary>
    /// Right "bat".
    /// </summary>
    private readonly Bat batOnTheRightControlledByHumanOrTrainer;

    /// <summary>
    /// Implements a 2 player scoreboard.
    /// </summary>
    private readonly ScoreBoard scoreBoard = new();

    /// <summary>
    /// Pen for drawing the net (dashed line).
    /// </summary>
    private static readonly Pen penNet = new(Color.FromArgb(100, 255, 255, 255), 1);

    /// <summary>
    /// Pen for drawing the cursor, which is represented as a red square.
    /// </summary>
    private static readonly Pen penCursor = new(Color.FromArgb(100, 255, 0, 0));

    /// <summary>
    /// The target position for the right hand bat.
    /// </summary>
    private int yTarget;

    /// <summary>
    /// A "ball" for this game.
    /// </summary>
    private Ball ball;

    /// <summary>
    /// Where the cursor is (mouse move) or where the auto mode positions it.
    /// </summary>
    private Point cursorPosition = new();

    /// <summary>
    /// The current training item (assigned when the ball reaches rhs).
    /// </summary>
    private TrainingDataItem? currentTrainingDataItem = null;

    /// <summary>
    /// The neural network controlling the bat.
    /// </summary>
    private readonly NeuralNetwork neuralNetworkControllingLeftBat;

    /// <summary>
    /// In auto-mode, it's playing against itself. The bat on the right is a "ball-tracker" not AI.
    /// </summary>
    private bool inAutoMode = true;

    /// <summary>
    /// Perpendicular hits are a bit of a waste of time, so we mostly let it choose to hit with the outer edges.
    /// </summary>
    private int offsetChosenByTrainerToStopPerpendicularHits = 0;

    /// <summary>
    /// 
    /// </summary>
    private bool expectArrivalLeft = false;

    /// <summary>
    /// 
    /// </summary>
    private readonly ImageFrameToXYBrain frameToXYBrain = new();

    /// <summary>
    /// Used to ensure that we ignore timer ticks if we are busy in an existing timer.
    /// </summary>
    private static bool inTimer = false;

    /// <summary>
    /// True until the network has been pre-trained (or loaded from the weights file).
    /// </summary>
    private bool needsPretraining = false;

    /// <summary>
    /// Error (px) of the network's prediction for the last 50 real arrivals, measured BEFORE learning from each one.
    /// </summary>
    private readonly Queue<double> liveErrorsPx = new();

    /// <summary>
    /// Zone (0 = centre .. 4 = tip) of the bat that the last 50 returned balls hit; shows whether we are offsetting.
    /// </summary>
    private readonly Queue<int> recentHitZones = new();

    /// <summary>
    /// Until enough real balls have been seen, assume roughly what the simulated test error was.
    /// </summary>
    private double ExpectedErrorPx() => liveErrorsPx.Count >= 10 ? liveErrorsPx.Average() : 3.0;

    /// <summary>
    /// Constructor.
    /// </summary>
#pragma warning disable CS8618 // SPURIOUS WARNING. NewBall() populates it.
    public FormPong()
#pragma warning restore CS8618 // SPURIOUS WARNING. NewBall() populates it.
    {
        InitializeComponent();

        neuralNetworkControllingLeftBat = new(layerDefinition: new int[] { BallPredictor.FeatureCount, 32, 16, 1 }); // ball features => bat Y

        Debug.Assert(pictureBoxDisplay.Width == Vision.CourtWidth && pictureBoxDisplay.Height == Vision.CourtHeight, "Vision.CourtWidth/Height must match pictureBoxDisplay");

        needsPretraining = !neuralNetworkControllingLeftBat.Load(WeightsFile);
        neuralNetworkControllingLeftBat.LearningRate = 0.005f; // gentle: it is refining, not starting from scratch

        batOnTheLeftControlledByAI = new(Side.Left, pictureBoxDisplay.Width, pictureBoxDisplay.Height);
        batOnTheRightControlledByHumanOrTrainer = new(Side.Right, pictureBoxDisplay.Width, pictureBoxDisplay.Height);
        yTarget = batOnTheRightControlledByHumanOrTrainer.Y;

        CreateNewBall();

        penNet.DashPattern = [5f, 5f];

        // the timer starts in OnShown, after any pre-training.
    }

    /// <summary>
    /// First run: teach the network from simulated trajectories (seconds), then cache the weights.
    /// </summary>
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        if (needsPretraining)
        {
            Pretrainer.Train(neuralNetworkControllingLeftBat, c_pretrainSamples, 3000, c_pretrainEpochs, (epochDone, mae, hit) =>
            {
                Text = $"Pong 6 - pre-training {epochDone}/{c_pretrainEpochs}: test error {mae:F1}px, would hit {hit:P0}";
                Debug.WriteLine(Text);
                Application.DoEvents();
            });

            Directory.CreateDirectory(Path.GetDirectoryName(WeightsFile)!);
            neuralNetworkControllingLeftBat.Save(WeightsFile);
            needsPretraining = false;
        }

        UpdateTitle();
        timer1.Enabled = true;
    }

    /// <summary>
    /// Title bar: epoch plus how well the network has predicted real balls it had not yet learned from.
    /// </summary>
    private void UpdateTitle()
    {
        string live = liveErrorsPx.Count == 0
            ? ""
            : $" | last {liveErrorsPx.Count}: avg error {liveErrorsPx.Average():F1}px, in bat reach {liveErrorsPx.Count(e => e <= Bat.c_halfTheBatLength) * 100 / liveErrorsPx.Count}%";

        string zones = recentHitZones.Count == 0 ? "" : $" | avg hit zone {recentHitZones.Average():F1} (0 = centre, 4 = tip)";

        Text = $"Pong 6 - Epoch {epoch}{live}{zones}";
    }

    /// <summary>
    /// Ball reached bat on left-hand-side. Did it hit the bat?
    /// </summary>
    private void Ball_BallReachedBatLeft()
    {
        expectArrivalLeft = false;

        int whereBallHitBat = batOnTheLeftControlledByAI.BallHitBat((int)Math.Round(ball.Y));

        // The item holds what the eyes saw once the ball left the right bat; now we learn where it actually arrived.
        if (currentTrainingDataItem is not null && currentTrainingDataItem.YPositionOfTheBallWhenItArrivesOnLeftBatLine == -1)
        {
            currentTrainingDataItem.YPositionOfTheBallWhenItArrivesOnLeftBatLine = ball.Y;

            // don't learn from items whose snapshots are incomplete, or that end up in the dead-zone (the bat can't get there anyway).
            bool reachable = ball.Y > Bat.c_deadZone + Bat.c_halfTheBatLength && ball.Y < pictureBoxDisplay.Height - Bat.c_deadZone - Bat.c_halfTheBatLength;

            if (currentTrainingDataItem.FramesComplete && currentTrainingDataItem.VisionOk && reachable)
            {
                LearnFromRealArrival(currentTrainingDataItem);
            }

            currentTrainingDataItem = null;
        }

        pictureBoxFrame1.Image?.Dispose();
        pictureBoxFrame1.Image = null;

        // maxValue = missed or  ball is behind bat
        if (whereBallHitBat == int.MaxValue)
        {
            // left player missed, so right player gets a point.
            scoreBoard.RightPlayerScored();

            // lob a new ball
            CreateNewBall();
            return;
        }

        recentHitZones.Enqueue(Math.Abs(whereBallHitBat));
        while (recentHitZones.Count > 50) recentHitZones.Dequeue();

        // ball hit the bat, ensure ball starts to right of left bat
        ball.X = Bat.c_batDistanceFromEdgeX + 4;

        // inverts the direction and makes it go off in a hit centric direction
        ball.BounceBallOffBat(whereBallHitBat);
        ++round;

        SetOffsetOfTrainer();
    }

    /// <summary>
    /// The network predicted a bat position before the ball arrived; now we know the truth. Record how wrong it was
    /// (a genuine out-of-sample measurement, since it has never seen this ball), then learn from it.
    /// </summary>
    private void LearnFromRealArrival(TrainingDataItem item)
    {
        double predicted = GetPosFromNN(item);
        double errorPx = Math.Abs(predicted - item.YPositionOfTheBallWhenItArrivesOnLeftBatLine);

        liveErrorsPx.Enqueue(errorPx);
        while (liveErrorsPx.Count > 50) liveErrorsPx.Dequeue();

        Trainer.AddToTrainingData(item);

        // A bounded number of steps. Mix in the new item, a remembered real one, and a fresh simulated one so that
        // fitting this ball doesn't make the network forget the others.
        for (int step = 0; step < 100 && Math.Abs(GetPosFromNN(item) - item.YPositionOfTheBallWhenItArrivesOnLeftBatLine) > 1; step++)
        {
            neuralNetworkControllingLeftBat.BackPropagate(item.GetTrainingData(), GetOutput(item));
            ImproveByTrainingAsTheBallMoves();
        }

        RefreshErrorsForVisualiser(item);
        VisuallyShowLearningError();
        UpdateTitle();
    }

    /// <summary>
    /// Recomputes what the network currently says for every remembered real item (for the visualiser only).
    /// </summary>
    private void RefreshErrorsForVisualiser(TrainingDataItem current)
    {
        if (!LearningVisualiser.Enabled) return;

        for (int i = 0; i < Trainer.Count; i++)
        {
            TrainingDataItem tdi = Trainer.TrainingData[i];

            tdi.lastBatPositionYOutputByAI = GetPosFromNN(tdi);
            tdi.lastBatExpectedYPosition = tdi.YPositionOfTheBallWhenItArrivesOnLeftBatLine;
        }

        current.lastBatPositionYOutputByAI = GetPosFromNN(current);
        current.lastBatExpectedYPosition = current.YPositionOfTheBallWhenItArrivesOnLeftBatLine;
    }

    /// <summary>The bat Y (px) the network wants for this item.</summary>
    private int GetPosFromNN(TrainingDataItem tdi)
    {
        return (int)Math.Round(BallPredictor.NNToY(neuralNetworkControllingLeftBat.FeedForward(tdi.GetTrainingData())[0]));
    }

    /// <summary>The network output we want for this item: where the ball really arrived.</summary>
    private static double[] GetOutput(TrainingDataItem tdi)
    {
        return new[] { BallPredictor.YToNN(tdi.YPositionOfTheBallWhenItArrivesOnLeftBatLine) };
    }

    /// <summary>
    /// Draw a diagram showing which training points it is inaccurate at.
    /// Doing this slows the training. It doesn't need to inform you of how good/bad it's learning.
    /// But this kind of stuff makes the app more fun, and it's part of my aimlfun.com, so you've come to expect it.
    /// </summary>
    private void VisuallyShowLearningError()
    {
        // We create a training item when it hits the right bat. We update it when it reaches left (with position of ball).
        // If not populated, something is hugely broken in the logic.
        if (currentTrainingDataItem is null) throw new Exception("currentTrainingDataItem not populated"); 
        
        // TODO: OPTIMISE FLAG. DO THIS ONCE WHEN IT IS ENABLED=FALSE, AND DON'T DO IT UNTIL SET TRUE
        // OR CACHE THE IMAGE.
        pictureBoxLearningError.Image?.Dispose(); // discard before replacing
        pictureBoxLearningError.Image = LearningVisualiser.GetDiagram(currentTrainingDataItem, c_maxAllowedDiffInAIVsExpected);
       
        Application.DoEvents();
    }

    /// <summary>
    /// Ball reached bat on right-hand-side. Did it hit the bat? 
    /// </summary>
    private void Ball_BallReachedBatRight()
    {
        if (expectArrivalLeft) Debug.WriteLine("Ball reached the right bat again before reaching the left one"); // bounced within itself

        // determine where the ball hit the bat, MaxValue means "missed" bat
        int whereBallHitBat = batOnTheRightControlledByHumanOrTrainer.BallHitBat((int)Math.Round(ball.Y));

        // missed or is to right of the bat.
        if (whereBallHitBat == int.MaxValue || ball.X > pictureBoxDisplay.Width - Bat.c_batDistanceFromEdgeX + 3)
        {
            scoreBoard.LeftPlayerScored();

            CreateNewBall();
            return;
        }

        expectArrivalLeft = true;

        ball.X = pictureBoxDisplay.Width - (Bat.c_batDistanceFromEdgeX + 7);

        ball.BounceBallOffBat(whereBallHitBat);
        ++round;

        // track item
        currentTrainingDataItem = new TrainingDataItem
        {
            frame = 0,
            round = round,
            epoch = epoch,
            xVelocityOfBallInDirectionOfLeftBat = ball.dx,
            yVelocityOfBallInDirectionOfLeftBat = ball.dy,
            YPositionOfTheBallWhenItArrivesOnRightBatLine = ball.Y,
            // the one below is stored when it reaches the "left" bat / goal line.
            YPositionOfTheBallWhenItArrivesOnLeftBatLine = -1
        };
    }

    /// <summary>
    /// Creates a new ball.
    /// </summary>
    private void CreateNewBall()
    {
        ++epoch;

        round = 1;
        ball = new Ball(pictureBoxDisplay.Width, pictureBoxDisplay.Height, BallDirection.LeftToRight);

        // events for when the ball reaches either bat
        ball.BallReachedBatLeft += Ball_BallReachedBatLeft;
        ball.BallReachedBatRight += Ball_BallReachedBatRight;

        SetOffsetOfTrainer(); // picks a random offset so bat doesn't return perpendicular balls

        UpdateTitle();

        expectArrivalLeft = false;
    }

    /// <summary>
    /// For each returned or new ball, the trainer picks an offset.
    /// </summary>
    private void SetOffsetOfTrainer()
    {
        int delta = RandomNumberGenerator.GetInt32(-Bat.c_halfTheBatLength, Bat.c_halfTheBatLength);

        offsetChosenByTrainerToStopPerpendicularHits = delta;
    }

    /// <summary>
    /// Frame by frame animation, using a timer.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void Timer1_Tick(object sender, EventArgs e)
    {
        if (inTimer) return; // skip this tick, we're busy.

        inTimer = true; // block timer ticks.

        try
        {
            if (currentTrainingDataItem != null) currentTrainingDataItem.frame++;

            MoveBallAndBothBats();

            UpdateVideoDisplay();

            SnapshotImage();

            // train every frame!
            ImproveByTrainingAsTheBallMoves(); // as the training points increase, performance will suffer
        }
        finally
        {
            inTimer = false;
        }
    }

    /// <summary>
    /// At the chosen frames, photograph the ball and record where it is. Frames are counted from the ball leaving the right bat.
    /// </summary>
    private void SnapshotImage()
    {
        TrainingDataItem? item = currentTrainingDataItem;

        if (item is null) return;

        if (item.frame != Vision.Frame1 && item.frame != Vision.Frame2 && item.frame != Vision.Frame3) return;

        double[]? xy = frameToXYBrain.AIMapImageIntoXY(AIviewFromAboveAndDecideResponse30x262pixels(snapShotImageTakenOfTheTennisCourtContainingTheBall));

        // The ball must be visible in every snapshot, and snapshots 2 and 3 are drawn on top of the earlier ones.
        if (xy is null || (item.frame != Vision.Frame1 && pictureBoxFrame1.Image is null))
        {
            item.VisionOk = false;
            Debug.WriteLine($"Ball not visible in snapshot at frame {item.frame}; this ball will not be learned from.");
            return;
        }

        // we're at frame 1, snapshot it
        if (item.frame == Vision.Frame1)
        {
            Bitmap b = new(snapShotImageTakenOfTheTennisCourtContainingTheBall);
            using Graphics g = Graphics.FromImage(b);
            g.DrawString($"1. ({xy[0]},{xy[1]})", new Font("Arial", 7), Brushes.White, new PointF(10, 10));

            pictureBoxFrame1.Image?.Dispose();
            pictureBoxFrame1.Image = b;
            item.frame1X = xy[0];
            item.frame1Y = xy[1];

            return;
        }

        // we're at frame 2, snapshot it
        if (item.frame == Vision.Frame2)
        {
            Bitmap b = MergeImage((Bitmap)pictureBoxFrame1.Image!, new(snapShotImageTakenOfTheTennisCourtContainingTheBall));

            using Graphics g = Graphics.FromImage(b);
            g.DrawString($"2. ({xy[0]},{xy[1]})", new Font("Arial", 7), Brushes.White, new PointF(10, 20));

            pictureBoxFrame1.Image?.Dispose();
            pictureBoxFrame1.Image = b;

            item.frame2X = xy[0];
            item.frame2Y = xy[1];

            return;
        }

        // frame 3: we now know enough to predict.

        Bitmap bFinal = MergeImage((Bitmap)pictureBoxFrame1.Image!, new(snapShotImageTakenOfTheTennisCourtContainingTheBall));

        item.frame3X = xy[0];
        item.frame3Y = xy[1];
        item.FramesComplete = item.VisionOk;
        item.lastBatPositionYOutputByAI = GetPosFromNN(item);
        item.AimBatY = ReturnPolicy.Choose(item.lastBatPositionYOutputByAI, item.frame2X, item.frame3X, ReturnPolicy.MaxOffsetFor(ExpectedErrorPx())).BatY;
        item.lastBatExpectedYPosition = -1; // unknown until the ball arrives

        using Graphics gFinal = Graphics.FromImage(bFinal);
        gFinal.DrawString($"3. ({xy[0]},{xy[1]})", new Font("Arial", 7), Brushes.White, new PointF(10, 30));

        DrawCrosses(gFinal); // prove AI returned correct position

        pictureBoxFrame1.Image?.Dispose();
        pictureBoxFrame1.Image = bFinal;

        VisuallyShowLearningError();
    }

    private void DrawCrosses(Graphics g)
    {
        g.DrawLine(Pens.Red, (int)currentTrainingDataItem.frame1X - 2, (int)currentTrainingDataItem.frame1Y - 2, (int)currentTrainingDataItem.frame1X + 2, (int)currentTrainingDataItem.frame1Y + 2);
        g.DrawLine(Pens.Red, (int)currentTrainingDataItem.frame2X - 2, (int)currentTrainingDataItem.frame2Y - 2, (int)currentTrainingDataItem.frame2X + 2, (int)currentTrainingDataItem.frame2Y + 2);
        g.DrawLine(Pens.Red, (int)currentTrainingDataItem.frame3X - 2, (int)currentTrainingDataItem.frame3Y - 2, (int)currentTrainingDataItem.frame3X + 2, (int)currentTrainingDataItem.frame3Y + 2);

        g.DrawLine(Pens.Red, (int)currentTrainingDataItem.frame1X + 2, (int)currentTrainingDataItem.frame1Y - 2, (int)currentTrainingDataItem.frame1X - 2, (int)currentTrainingDataItem.frame1Y + 2);
        g.DrawLine(Pens.Red, (int)currentTrainingDataItem.frame2X + 2, (int)currentTrainingDataItem.frame2Y - 2, (int)currentTrainingDataItem.frame2X - 2, (int)currentTrainingDataItem.frame2Y + 2);
        g.DrawLine(Pens.Red, (int)currentTrainingDataItem.frame3X + 2, (int)currentTrainingDataItem.frame3Y - 2, (int)currentTrainingDataItem.frame3X - 2, (int)currentTrainingDataItem.frame3Y + 2);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="image1"></param>
    /// <param name="image2"></param>
    /// <returns></returns>
    private static Bitmap MergeImage(Bitmap image1, Bitmap image2)
    {
        Bitmap b = new(image1);
        Bitmap b2 = new(image2);

        b2.MakeTransparent(Color.Black);
        using Graphics g = Graphics.FromImage(b);
        g.DrawImageUnscaled(b2, 0, 0);
        g.Flush();
        b2.Dispose();

        return b;
    }

    /// <summary>
    /// Makes an empty display, draws the net, 2xbats + ball + score
    /// </summary>
    private void UpdateVideoDisplay()
    {
        Bitmap b = new(pictureBoxDisplay.Width, pictureBoxDisplay.Height);

        using Graphics g = Graphics.FromImage(b);
        g.Clear(Color.Black);

        // draw a square where the cursor is horizontally
        if (!inAutoMode) g.DrawRectangle(penCursor, batOnTheRightControlledByHumanOrTrainer.X - 3, cursorPosition.Y - 3, 6, 6);

        // draw both bats + ball
        batOnTheLeftControlledByAI.Draw(g);
        batOnTheRightControlledByHumanOrTrainer.Draw(g);

        if (currentTrainingDataItem != null) batOnTheRightControlledByHumanOrTrainer.DrawDottedPosition(g, (float)currentTrainingDataItem.YPositionOfTheBallWhenItArrivesOnRightBatLine);

        // ball gets drawn, then moved. This leads to a more Pong like visual plus a faster ball.
        ball.Draw(g);
        g.Flush();

        // The eyes are a window onto the court just left of the right bat. It is refreshed every frame: it used to refresh
        // only while the ball was right of centre, so at higher speeds the 3rd snapshot was a stale picture.
        Bitmap eyes = new((int)Vision.WidthOfEyes, pictureBoxDisplay.Height);

        using (Graphics graphicsEyesRight = Graphics.FromImage(eyes))
        {
            graphicsEyesRight.DrawImageUnscaled(b, -Vision.WindowOriginX, 0, (int)Vision.WidthOfEyes, eyes.Height);
        }

        snapShotImageTakenOfTheTennisCourtContainingTheBall?.Dispose();
        snapShotImageTakenOfTheTennisCourtContainingTheBall = eyes;

        // draw net (goes vertical)
        g.DrawLine(penNet, pictureBoxDisplay.Width / 2, 0, pictureBoxDisplay.Width / 2, pictureBoxDisplay.Height);

        scoreBoard.Draw(g, pictureBoxDisplay.Width);

        if (c_drawDebugLines)
        {
#pragma warning disable CS0162 // Unreachable code detected. The code is reached if you turn on debug.
            DrawDebugLines(g);
#pragma warning restore CS0162 // Unreachable code detected. The code is reached if you turn on debug.
        }

        pictureBoxDisplay.Image?.Dispose();
        pictureBoxDisplay.Image = b;
    }

    /// <summary>
    /// Draw debug lines.
    /// </summary>
    /// <param name="g"></param>
    private void DrawDebugLines(Graphics g)
    {
        using Pen penDebugLine = new(Color.FromArgb(70, 255, 0, 0));
        penDebugLine.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;

        // lines where bats are
        g.DrawLine(penDebugLine, Bat.c_batDistanceFromEdgeX + 4, 0, Bat.c_batDistanceFromEdgeX + 4, pictureBoxDisplay.Height);
        g.DrawLine(penDebugLine, pictureBoxDisplay.Width - Bat.c_batDistanceFromEdgeX - 4, 0, pictureBoxDisplay.Width - Bat.c_batDistanceFromEdgeX - 4, pictureBoxDisplay.Height);

        // lines showing "dead" zone that bat cannot enter
        g.DrawLine(penDebugLine, 0, Bat.c_deadZone, pictureBoxDisplay.Width, Bat.c_deadZone);
        g.DrawLine(penDebugLine, 0, pictureBoxDisplay.Height - Bat.c_deadZone, pictureBoxDisplay.Width, pictureBoxDisplay.Height - Bat.c_deadZone);
    }

    /// <summary>
    /// Moves the ball and bats.
    /// </summary>
    private void MoveBallAndBothBats()
    {
        // now move the ball.
        ball.Move();

        if (inAutoMode)
        {
            cursorPosition.Y = (int)ball.Y + offsetChosenByTrainerToStopPerpendicularHits;
            yTarget = cursorPosition.Y;
        }

        // either human controlled, or auto
        batOnTheRightControlledByHumanOrTrainer.Move(yTarget);
        batOnTheRightControlledByHumanOrTrainer.Move(yTarget);

        // neural network controls left bat, once it has seen enough of the ball to predict.
        if (currentTrainingDataItem is { FramesComplete: true, VisionOk: true })
        {
            // not simply under the ball: the aim point is offset so the return leaves at an angle (see ReturnPolicy)
            int target = currentTrainingDataItem.AimBatY;

            batOnTheLeftControlledByAI.Move(target);
            batOnTheLeftControlledByAI.Move(target);
        }
    }

    /// <summary>
    /// We pick a random training point, and backpropagate, once per time the ball moves. This
    /// in theory refines the training.
    /// </summary>
    private void ImproveByTrainingAsTheBallMoves()
    {
        // one remembered real ball (the old code could never pick the last one)...
        if (Trainer.TrainingData.Count > 0)
        {
            TrainingDataItem real = Trainer.TrainingData[RandomNumberGenerator.GetInt32(0, Trainer.TrainingData.Count)];
            neuralNetworkControllingLeftBat.BackPropagate(real.GetTrainingData(), GetOutput(real));
        }

        // ...and one fresh simulated ball, so fitting the real ones never makes it forget the general rule.
        TrainingDataItem? simulated = SyntheticData.Generate();
        if (simulated is not null) neuralNetworkControllingLeftBat.BackPropagate(simulated.GetTrainingData(), GetOutput(simulated));
    }

    /// <summary>
    /// If automode, it tracks the ball automatically. 
    /// If !automode, user is expected to use the mouse to control the bat.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void PictureBoxDisplay_MouseMove(object sender, MouseEventArgs e)
    {
        if (!inAutoMode)
        {
            Control? c = FindControlAtCursor(this);

            if (c == null || c != pictureBoxDisplay) return;

            if (this.Focused) yTarget = e.Y;
            cursorPosition = e.Location;
        }
        else
        {
            cursorPosition.Y = (int)(ball.Y + RandomNumberGenerator.GetInt32(-10, 10));
            yTarget = cursorPosition.Y;
        }
    }

    /// <summary>
    /// Handle special keys.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void FormPong_KeyDown(object sender, KeyEventArgs e)
    {
        // "P" pauses game.
        if (e.KeyCode == Keys.P) timer1.Enabled = !timer1.Enabled;

        // "S" cycles speed.
        if (e.KeyCode == Keys.S) StepThroughSpeeds();

        // "V" visualiser.
        if (e.KeyCode == Keys.V) LearningVisualiser.Enabled = !LearningVisualiser.Enabled;

        // "A" turns on auto-return mode.
        if (e.KeyCode == Keys.A)
        {
            inAutoMode = !inAutoMode;
            if (inAutoMode) Cursor.Hide(); else Cursor.Show();
        }
    }

    /// <summary>
    /// Steps thru the speeds by changing the timer interval.
    /// </summary>
    internal void StepThroughSpeeds()
    {
        var newInterval = timer1.Interval switch
        {
            5 => 20,
            20 => 100,
            100 => 1000,
            _ => 5,
        };

        timer1.Interval = newInterval;
    }

    /// <summary>
    /// Works out from the control hierarchy where the point is situated.
    /// </summary>
    /// <param name="container"></param>
    /// <param name="pos"></param>
    /// <returns></returns>
    public static Control? FindControlAtPoint(Control container, Point pos)
    {
        Control? child;

        foreach (Control c in container.Controls)
        {
            if (c.Visible && c.Bounds.Contains(pos))
            {
                child = FindControlAtPoint(c, new Point(pos.X - c.Left, pos.Y - c.Top));

                if (child == null)
                    return c;
                else
                    return child;
            }
        }

        return null;
    }

    /// <summary>
    /// Returns a control at the current cursor position of "null" if not within form.
    /// </summary>
    /// <param name="form"></param>
    /// <returns></returns>
    public static Control? FindControlAtCursor(Form form)
    {
        Point pos = Cursor.Position;

        if (form.Bounds.Contains(pos)) return FindControlAtPoint(form, form.PointToClient(pos));

        return null;
    }

    /// <summary>
    /// Returns ALL the pixels as a "double" array. The element containing a 1 has part the ball.
    /// </summary>
    /// <returns></returns>
    internal static double[] AIviewFromAboveAndDecideResponse30x262pixels(Bitmap frameImage)
    {
        ByteAccessibleBitmap accessibleBitmap = new(frameImage);

        int width = frameImage.Width;
        int height = frameImage.Height;

        double[] vertPixels = new double[262];
        double[] horizPixels = new double[width];

        int whitePixelCount = 0;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                // white is red+green+blue, we only need one of them to detect presence of pixel
                if (accessibleBitmap.GetRedChannelPixel(x, y) != 0)
                {
                    vertPixels[y] = 1; // 1 = ball (white pixel), 0 = no pixel.
                    horizPixels[x] = 1; // vertical depth

                    ++whitePixelCount;
                }
            }
        }

        List<double> pix = new(horizPixels);
        pix.AddRange(vertPixels);

        return pix.ToArray();
    }

}