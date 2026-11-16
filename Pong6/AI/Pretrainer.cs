using System.Security.Cryptography;
using Pong.GameArea;

namespace Pong.AI;

/// <summary>
/// Trains the bat network on simulated trajectories, with a held-out test set so progress is measurable.
/// </summary>
internal static class Pretrainer
{
    /// <summary>
    /// Mean absolute error (px) and the fraction of test items where the bat would have been under the ball.
    /// </summary>
    internal static (double meanAbsErrorPx, double hitRate) Evaluate(NeuralNetwork net, List<TrainingDataItem> test)
    {
        double sum = 0;
        int hits = 0;

        foreach (TrainingDataItem t in test)
        {
            double predicted = BallPredictor.NNToY(net.FeedForward(t.GetTrainingData())[0]);
            double err = Math.Abs(predicted - t.YPositionOfTheBallWhenItArrivesOnLeftBatLine);
            sum += err;

            if (err <= Bat.c_halfTheBatLength) ++hits;
        }

        return (sum / test.Count, (double)hits / test.Count);
    }

    /// <summary>
    /// The same measure for the closed-form baseline, to show what the network has to beat.
    /// </summary>
    internal static (double meanAbsErrorPx, double hitRate) EvaluateBaseline(List<TrainingDataItem> test)
    {
        double sum = 0;
        int hits = 0;

        foreach (TrainingDataItem t in test)
        {
            double predicted = BallPredictor.BaselineLandingY(t.frame2X, t.frame2Y, t.frame3X, t.frame3Y);
            double err = Math.Abs(predicted - t.YPositionOfTheBallWhenItArrivesOnLeftBatLine);
            sum += err;
            if (err <= Bat.c_halfTheBatLength) ++hits;
        }

        return (sum / test.Count, (double)hits / test.Count);
    }

    /// <summary>
    /// Back-propagates every item once, in random order.
    /// </summary>
    internal static void RunEpoch(NeuralNetwork net, List<TrainingDataItem> data)
    {
        int[] order = [.. Enumerable.Range(0, data.Count)];

        for (int i = order.Length - 1; i > 0; i--) // Fisher-Yates shuffle
        {
            int j = RandomNumberGenerator.GetInt32(0, i + 1);
            (order[i], order[j]) = (order[j], order[i]);
        }

        foreach (int i in order)
        {
            net.BackPropagate(data[i].GetTrainingData(), new[] { BallPredictor.YToNN(data[i].YPositionOfTheBallWhenItArrivesOnLeftBatLine) });
        }
    }

    /// <summary>
    /// Trains for the given number of epochs, decaying the learning rate, and reports test error after each epoch.
    /// </summary>
    internal static void Train(NeuralNetwork net, int trainingSamples, int testSamples, int epochs, Action<int, double, double>? progress = null)
    {
        List<TrainingDataItem> train = SyntheticData.Generate(trainingSamples);
        List<TrainingDataItem> test = SyntheticData.Generate(testSamples);

        float start = 0.02f, end = 0.002f;

        for (int epoch = 1; epoch <= epochs; epoch++)
        {
            net.LearningRate = start + (end - start) * (epoch - 1) / Math.Max(1, epochs - 1);

            RunEpoch(net, train);

            (double mae, double hit) = Evaluate(net, test);
            progress?.Invoke(epoch, mae, hit);
        }

        net.LearningRate = 0.005f; // gentle rate for online learning thereafter
    }
}
