namespace Pong.AI;

/// <summary>
/// Represents a Training Data Item
/// </summary>
internal class TrainingDataItem
{
    internal const string c_header = "epoch,round,frame1X,frame1Y,frame2X,frame2Y,frame3X,frame3Y,YPositionOfTheBallWhenItArrivesOnLeftBatLine,YPositionOfTheBallWhenItArrivesOnRightBatLine,xVelocityOfBallInDirectionOfLeftBat,yVelocityOfBallInDirectionOfLeftBat";
    
    #region OBJECT PROPERTIES
    /// <summary>
    /// Epoch the training data item belongs to.
    /// </summary>
    internal int epoch = 0;

    /// <summary>
    /// Frame this training data is on. Starts on 1, increments then the 2nd image is snapshotted.
    /// </summary>
    internal int frame = 0;

    /// <summary>
    /// Increments if the ball was returned.
    /// </summary>
    internal int round = 0;

    /// <summary>All three snapshots have been taken.</summary>
    internal bool FramesComplete;

    /// <summary>Where the left bat's centre is sent: the predicted arrival Y, deliberately offset (see <see cref="ReturnPolicy"/>).</summary>
    internal int AimBatY = -1;

    /// <summary>false if the ball could not be seen in one of the snapshots; such items must not be learned from.</summary>
    internal bool VisionOk = true;

    internal double frame1X;
    internal double frame2X;
    internal double frame3X;
    
    internal double frame1Y;
    internal double frame2Y;
    internal double frame3Y;

    /// <summary>
    /// 
    /// </summary>
    internal float xVelocityOfBallInDirectionOfLeftBat;
    
    /// <summary>
    /// 
    /// </summary>
    internal float yVelocityOfBallInDirectionOfLeftBat;
    
    /// <summary>
    /// 
    /// </summary>
    internal double lastBatExpectedYPosition;
    
    /// <summary>
    /// 
    /// </summary>
    internal double lastBatPositionYOutputByAI;

    /// <summary>
    /// The "Y" position of the ball when it arrives on the left.
    /// </summary>
    internal double YPositionOfTheBallWhenItArrivesOnRightBatLine;
    
    /// <summary>
    /// 
    /// </summary>
    internal double YPositionOfTheBallWhenItArrivesOnLeftBatLine;
    #endregion

    /// <summary>
    /// Returns the training data item as a string.
    /// </summary>
    /// <returns></returns>
    public override string? ToString()
    {
        return $"{epoch},{round},{frame1X},{frame1Y},{frame2X},{frame2Y},{frame3X},{frame3Y},{Math.Round(YPositionOfTheBallWhenItArrivesOnLeftBatLine,2)},{Math.Round(YPositionOfTheBallWhenItArrivesOnRightBatLine, 2)},{Math.Round(xVelocityOfBallInDirectionOfLeftBat, 2)},{Math.Round(yVelocityOfBallInDirectionOfLeftBat, 2)}";
    }

    /// <summary>
    /// Network inputs derived from the three observed ball positions (see <see cref="BallPredictor.Features"/>).
    /// </summary>
    /// <returns></returns>
    internal double[] GetTrainingData()
    {
        return BallPredictor.Features(frame1X, frame1Y, frame2X, frame2Y, frame3X, frame3Y);
    }

}