# Pong

AI Pong based on frames from screen.

4 years ago. One of my first few neural network apps was ML Pong. V1 was backpropagation: ball position => bat position. But that doesn't work. Balls bounce, and tracking a ball in that manner can leave the bat incapable of reaching the ball.

V3 worked, treating it as a small regression problem. Four numbers go in and one comes out: opposing bat Y, ball Y at hit, dx and dy. The label is the ball's Y (when it reaches the left line, i.e. where to place the bat), and the game generates that label. At 260 balls it was returning successfully, each rally lengthened.

**So why make this?**

It felt cheating feeding in values from the bat/ball directly. I finally got around to reworking an idea that seemed sound, but didn't seem to work as well as I expected.

This v6 snapshots three frames containing the ball. By reading the y position at known frames, the position of the bat can be determined. It even trains as the ball moves!

The learning rate is good, closing the original challenge. 

But we're not done yet. Coming soon is one that plays using RL and CNNs!
