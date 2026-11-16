using System.Diagnostics;

namespace Pong.AI;

internal static class Trainer
{
    #region CONSTANTS
    /// <summary>
    /// Where we write / expect the training data.
    /// </summary>
    private const string c_trainingDataFilePath = @"d:\temp\pong\pong2.txt";
    #endregion

    #region STATIC VARIABLES
    /// <summary>
    /// List of training items recorded (or loaded and added to).
    /// </summary>
    private static readonly List<TrainingDataItem> s_trainingData = new();

    /// <summary>
    /// Stores a mapping of training data items by the "image" of it.
    /// </summary>
    private static readonly Dictionary<string, TrainingDataItem> s_uniqueTrainingDataCapture = new();
    #endregion

    /// <summary>
    /// Returns the number of items.
    /// </summary>
    internal static int Count
    {
        get { return s_trainingData.Count; }
    }

    /// <summary>
    /// Returns the training data.
    /// </summary>
    internal static List<TrainingDataItem> TrainingData
    {
        get
        {
            return s_trainingData;
        }
    }

    /// <summary>
    /// Stores the training data into a list, and then saves to file.
    /// </summary>
    internal static void AddToTrainingData(TrainingDataItem? item, bool save = true)
    {
        if (item is null) return;

        string hash = $"{item.frame1X}-{item.frame1Y}-{item.frame2X}-{item.frame2Y}-{item.frame3X}-{item.frame3Y}";

        if (s_uniqueTrainingDataCapture.ContainsKey(hash))
        {
            // nothing to learn: we've encountered a position we learnt previously, and have training data for
            if (Math.Round(s_uniqueTrainingDataCapture[hash].YPositionOfTheBallWhenItArrivesOnLeftBatLine, 2) == Math.Round(item.YPositionOfTheBallWhenItArrivesOnLeftBatLine, 2) &&
                Math.Round(s_uniqueTrainingDataCapture[hash].YPositionOfTheBallWhenItArrivesOnRightBatLine, 2) == Math.Round(item.YPositionOfTheBallWhenItArrivesOnRightBatLine, 2))
            {
                Debug.WriteLine("ALREADY IN TRAINING DATA - NOT ADDING");
                return;
            }

            Debug.WriteLine("TWO ITEMS HAVE SAME HASH");
            Debug.WriteLine($"PREVIOUS: {s_uniqueTrainingDataCapture[hash]}");
            Debug.WriteLine($"THIS: {item}");

            // if (1) xvel=-2.53 and (2) xvel=-2.49, then after 10 frames=> 2.53 / 24.9; both rounded = 25.
            // we'd need a lot of pixels in frame.
            // If they nearly arrive at same left pos, simply average. I know if a 3rd arrives, average will be (((#1+#2)/2)+#3)/2. Not great averaging an average.
            if (Math.Abs(item.YPositionOfTheBallWhenItArrivesOnLeftBatLine - s_uniqueTrainingDataCapture[hash].YPositionOfTheBallWhenItArrivesOnLeftBatLine) < 3)
            {
                s_uniqueTrainingDataCapture[hash].YPositionOfTheBallWhenItArrivesOnLeftBatLine = (s_uniqueTrainingDataCapture[hash].YPositionOfTheBallWhenItArrivesOnLeftBatLine +
                                                                                                  item.YPositionOfTheBallWhenItArrivesOnLeftBatLine) / 2;
                Debug.WriteLine("ALREADY IN TRAINING DATA WITHIN A SIMILAR YPOSITION");
                return;
            }

            // same picture, clearly different outcome (pixel quantisation makes different paths look identical).
            // Can't store both under one hash, and we shouldn't stop the game over it. Keep the first.
            Debug.WriteLine("ALREADY IN TRAINING DATA AMOUNT TOO DIFFERENT - IGNORED");
            return;
        }

        s_uniqueTrainingDataCapture.Add(hash, item);

        // expand training data 
        s_trainingData.Add(item);

        // rewriting the whole file on every add is O(n^2) disk I/O; save every 25.
        if (save && s_trainingData.Count % 25 == 0)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(c_trainingDataFilePath)!);
            File.WriteAllText(c_trainingDataFilePath, TrainingDataItem.c_header + "\n" +
                                                       string.Join("\n", s_trainingData));
        }
    }
}
