namespace Pong.AI;

/// <summary>
/// Visualiser for training current data item.
/// </summary>
internal static class LearningVisualiser
{
    /// <summary>
    /// Indicates whether to expend effort painting all the deviations or not.
    /// </summary>
    internal static bool Enabled = true;

    /// <summary>
    /// Draw a diagram showing which training points it is inaccurate at.
    /// </summary>
    /// <param name="currentTrainingDataItem"></param>
    /// <param name="maxAllowedDiffInAIVsExpected"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    internal static Bitmap GetDiagram(TrainingDataItem currentTrainingDataItem, float maxAllowedDiffInAIVsExpected)
    {
        // We create a training item when it hits the right bat. We update it when it reaches left (with position of ball).
        // If not populated, something is hugely broken in the logic.
        if (currentTrainingDataItem is null) throw new ArgumentNullException(nameof(currentTrainingDataItem), "currentTrainingDataItem not populated");

        // 262 Y positions ball can be vs. 262 Y positions of bat.
        Bitmap image = new(262, 262);

        using Graphics graphics = Graphics.FromImage(image);

        graphics.Clear(Color.Black);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
        graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;

        if(!Enabled)
        {
            WriteVisualiserIsDisabledInCentre(graphics);
            return image;
        }

        LabelXAxis(graphics);

        LabelYAxis(graphics);

        DrawGreyLineIndicatingWhereBallHitRightBat(currentTrainingDataItem, graphics);

        for (int i = 0; i < Trainer.TrainingData.Count; i++)
        {
            TrainingDataItem tdi = Trainer.TrainingData[i];

            // for the item that missed, draw a horizontal line indicating where it is currently at (neural-network output)
            if (tdi == currentTrainingDataItem)
            {
                DrawGreenAndRedLinesShowingActualAndExpectedForCurrentItemBeingTrained(graphics, tdi);
            }

            double errorDifferenceBetweenAIresultAndExpected = tdi.lastBatExpectedYPosition - tdi.lastBatPositionYOutputByAI;

            // if error is within acceptable ranges, we draw nothing (speeding up the training)
            if (Math.Abs(errorDifferenceBetweenAIresultAndExpected) > maxAllowedDiffInAIVsExpected)
            {
                DrawErrorDeviationBar(graphics, tdi, errorDifferenceBetweenAIresultAndExpected);
            }
        }

        graphics.Flush();

        return image;
    }

    /// <summary>
    /// Displays visualiser has been turned off.
    /// </summary>
    /// <param name="graphics"></param>
    private static void WriteVisualiserIsDisabledInCentre(Graphics graphics)
    {
        using Font f = new("Arial", 10);
        string label = "Visualiser: OFF";
        SizeF size = graphics.MeasureString(label, f);

        graphics.DrawString(label, f, Brushes.White, new PointF(131 - size.Width / 2, 131-size.Height));
    }

    /// <summary>
    /// Draws a fat bar showing where returned AI value differs from expected (above an acceptable threshold).
    /// </summary>
    /// <param name="graphics"></param>
    /// <param name="tdi"></param>
    /// <param name="errorDifferenceBetweenAIresultAndExpected"></param>
    private static void DrawErrorDeviationBar(Graphics graphics, TrainingDataItem tdi, double errorDifferenceBetweenAIresultAndExpected)
    {
        Color colour = Color.FromArgb(180,
                            errorDifferenceBetweenAIresultAndExpected < 0 ? 255 : 100, // -ve in red
                            100, // brightens the red/blue [adding green]
                            errorDifferenceBetweenAIresultAndExpected > 0 ? 255 : 100 // +ve in blue
                            );

        using Pen pen = new(colour, 3);

        graphics.DrawLine(pen,
                          (int)Math.Round(tdi.YPositionOfTheBallWhenItArrivesOnRightBatLine),
                          (int)Math.Round(tdi.lastBatExpectedYPosition),
                          (int)Math.Round(tdi.YPositionOfTheBallWhenItArrivesOnRightBatLine),
                          (int)Math.Round(tdi.lastBatPositionYOutputByAI));
    }

    /// <summary>
    /// Draw two horizontal lines (the one the AI returns, and the one we want the AI should return).
    /// </summary>
    /// <param name="graphics"></param>
    /// <param name="tdi"></param>
    private static void DrawGreenAndRedLinesShowingActualAndExpectedForCurrentItemBeingTrained(Graphics graphics, TrainingDataItem tdi)
    {
        // draw a horizontal red line indicating where the AI thinks the bat should be

        using Pen dottedRedLineIndicatingAIpositionOfBat = new(Color.FromArgb(60, 255, 0, 0));
        dottedRedLineIndicatingAIpositionOfBat.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;

        graphics.DrawLine(dottedRedLineIndicatingAIpositionOfBat,
                    0,
                    (int)Math.Round(tdi.lastBatPositionYOutputByAI ),
                    262f,
                    (int)Math.Round(tdi.lastBatPositionYOutputByAI ));

        // draw a horizontal green line indicating where the AI should position the bat (expected position).

        if (tdi.lastBatExpectedYPosition != -1)
        {
            using Pen dottedGreenLineIndicatingPositionBatShouldBe = new(Color.FromArgb(60, 0, 255, 0));
            dottedGreenLineIndicatingPositionBatShouldBe.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;

            graphics.DrawLine(dottedGreenLineIndicatingPositionBatShouldBe,
                        0,
                        (int)Math.Round(tdi.lastBatExpectedYPosition),
                        262f,
                        (int)Math.Round(tdi.lastBatExpectedYPosition));
        }
   }

    /// <summary>
    /// Draw a vertical line of where it hit the right bat, indicating the one we are fixing.
    /// </summary>
    /// <param name="currentTrainingDataItem"></param>
    /// <param name="graphics"></param>
    private static void DrawGreyLineIndicatingWhereBallHitRightBat(TrainingDataItem currentTrainingDataItem, Graphics graphics)
    {
        using Pen dottedGreyLineIndicationOfWhereBallHitRightBat = new(Color.FromArgb(40, 255, 255, 255));
        dottedGreyLineIndicationOfWhereBallHitRightBat.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;

        graphics.DrawLine(dottedGreyLineIndicationOfWhereBallHitRightBat,
                    (int)Math.Round(currentTrainingDataItem.YPositionOfTheBallWhenItArrivesOnRightBatLine), 0,
                    (int)Math.Round(currentTrainingDataItem.YPositionOfTheBallWhenItArrivesOnRightBatLine), 262f);            
    }

    /// <summary>
    /// Horizontal label at top of diagram
    /// </summary>
    /// <param name="graphics"></param>
    private static void LabelXAxis(Graphics graphics)
    {
        using Font f = new("Arial", 7);
        string label = "Y Position - Right Bat";
        SizeF size = graphics.MeasureString(label, f);
        
        graphics.DrawString(label, f, Brushes.White, new PointF(131 - size.Width / 2, 5));
    }

    /// <summary>
    /// Vertical text on lhs of diagram.
    /// </summary>
    /// <param name="graphics"></param>
    private static void LabelYAxis(Graphics graphics)
    {
        using Font f = new("Arial", 7);

        StringFormat stringFormat = new()
        {
            FormatFlags = StringFormatFlags.DirectionVertical
        };

        string label = "Y Position - Left Bat";
        SizeF size = graphics.MeasureString(label, f);
        graphics.DrawString(label, f, Brushes.White, new PointF(5, 131 - size.Width / 2), stringFormat);

    }
}
