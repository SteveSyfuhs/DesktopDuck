namespace DesktopDuck;

internal sealed class DucklingMotion
{
    public double Distance { get; internal set; }
    public double WalkingDistance { get; internal set; }
    public bool IsMoving { get; internal set; }
    internal double Pace { get; set; }
    internal double TargetPace { get; set; }
    internal double PaceChangeIn { get; set; }
}

internal sealed class FlockMotion
{
    public const double QuackHoldSeconds = 1.0;

    private readonly Random random;
    private readonly DucklingMotion[] ducklings = [new(), new(), new()];
    private double spacing;
    private double quackRemaining;

    public FlockMotion(Random? random = null)
    {
        this.random = random ?? Random.Shared;
        foreach (DucklingMotion duckling in ducklings)
        {
            ChoosePace(duckling);
            duckling.Pace = duckling.TargetPace;
        }
    }

    public IReadOnlyList<DucklingMotion> Ducklings => ducklings;
    public double LeaderDistance { get; private set; }
    public double LeaderWalkingDistance { get; private set; }
    public bool LeaderIsMoving { get; private set; }
    public bool IsGathering { get; private set; }
    public bool IsQuacking => quackRemaining > 0;

    public void Place(double leaderDistance, double newSpacing)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(newSpacing);
        for (int follower = 0; follower < ducklings.Length; follower++)
        {
            DucklingMotion duckling = ducklings[follower];
            double gap = spacing > 0 ? (LeaderDistance - duckling.Distance) / spacing : follower + 1;
            duckling.Distance = leaderDistance - gap * newSpacing;
        }
        LeaderDistance = leaderDistance;
        spacing = newSpacing;
    }

    public void BeginGathering()
    {
        IsGathering = true;
        LeaderIsMoving = false;
        quackRemaining = QuackHoldSeconds;
    }

    // Distances stay unwrapped so a follower cannot jump ahead when the leader completes a lap.
    public bool Tick(double elapsed, bool paused, double leaderSpeed)
    {
        quackRemaining = Math.Max(0, quackRemaining - elapsed);
        LeaderIsMoving = false;
        foreach (DucklingMotion duckling in ducklings)
        {
            duckling.IsMoving = false;
        }

        if (!paused)
        {
            if (!IsGathering)
            {
                double travel = leaderSpeed * elapsed;
                LeaderDistance += travel;
                LeaderWalkingDistance += travel;
                LeaderIsMoving = travel > 0;
            }

            double precedingDistance = LeaderDistance;
            foreach (DucklingMotion duckling in ducklings)
            {
                duckling.PaceChangeIn -= elapsed;
                if (duckling.PaceChangeIn <= 0)
                {
                    ChoosePace(duckling);
                }
                double targetPace = IsGathering ? 1.6 + duckling.TargetPace * 0.3 : duckling.TargetPace;
                duckling.Pace += (targetPace - duckling.Pace) * Math.Min(1, elapsed * 2.5);

                double nextDistance = Math.Min(precedingDistance - spacing,
                    duckling.Distance + leaderSpeed * duckling.Pace * elapsed);
                double travel = Math.Max(0, nextDistance - duckling.Distance);
                duckling.Distance += travel;
                duckling.WalkingDistance += travel;
                duckling.IsMoving = travel > 0;
                precedingDistance = duckling.Distance;
            }
        }

        double extraLag = 0;
        for (int follower = 0; follower < ducklings.Length; follower++)
        {
            extraLag = Math.Max(extraLag,
                LeaderDistance - ducklings[follower].Distance - (follower + 1) * spacing);
        }

        if (IsGathering)
        {
            if (!IsQuacking && extraLag < 0.001)
            {
                IsGathering = false;
            }
        }
        else if (!paused && extraLag > spacing * 2.5)
        {
            BeginGathering();
            return true;
        }

        return false;
    }

    private void ChoosePace(DucklingMotion duckling)
    {
        duckling.TargetPace = 0.35 + random.NextDouble() * 0.9;
        duckling.PaceChangeIn = 0.7 + random.NextDouble() * 1.8;
    }
}
