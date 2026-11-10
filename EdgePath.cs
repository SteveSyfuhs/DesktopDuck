namespace DesktopDuck;

internal readonly record struct EdgePose(double X, double Y, int Rotation);

internal static class EdgePath
{
    public static double Length(double width, double height) =>
        2 * (Math.Max(0, width) + Math.Max(0, height));

    public static EdgePose AtCornerHop(double distance, double width, double height, int leaderSize, int size)
    {
        EdgePose pose = AtSize(distance, width, height, leaderSize, size);
        if (width <= 0 || height <= 0)
        {
            return pose;
        }

        double length = Length(width, height);
        double wrapped = ((distance % length) + length) % length;
        double span = Math.Min(size * 0.25, Math.Min(width, height) / 4);
        ReadOnlySpan<double> corners = [0, width, width + height, 2 * width + height];
        foreach (double cornerDistance in corners)
        {
            double offset = wrapped - cornerDistance;
            if (offset > length / 2)
            {
                offset -= length;
            }
            else if (offset < -length / 2)
            {
                offset += length;
            }
            if (Math.Abs(offset) >= span)
            {
                continue;
            }

            EdgePose entry = AtSize(cornerDistance - span, width, height, leaderSize, size);
            EdgePose exit = AtSize(cornerDistance + span, width, height, leaderSize, size);
            bool rightCorner = cornerDistance == width || cornerDistance == width + height;
            bool bottomCorner = cornerDistance == 0 || cornerDistance == width;
            double inset = leaderSize - size;
            double cornerX = rightCorner ? width + inset : 0;
            double cornerY = bottomCorner ? height + inset : 0;
            double progress = (offset + span) / (2 * span);
            double remaining = 1 - progress;
            double lift = span * 0.55 * 4 * progress * remaining;

            return pose with
            {
                X = remaining * remaining * entry.X + 2 * remaining * progress * cornerX
                    + progress * progress * exit.X + (rightCorner ? -lift : lift),
                Y = remaining * remaining * entry.Y + 2 * remaining * progress * cornerY
                    + progress * progress * exit.Y + (bottomCorner ? -lift : lift)
            };
        }

        return pose;
    }

    public static EdgePose AtSize(double distance, double width, double height, int leaderSize, int size)
    {
        EdgePose pose = At(distance, width, height);
        double inset = leaderSize - size;

        // Share the leader's route length, but keep smaller feet against the same screen edge.
        return pose.Rotation switch
        {
            0 => pose with { X = pose.X + inset / 2, Y = pose.Y + inset },
            270 => pose with { X = pose.X + inset, Y = pose.Y + inset / 2 },
            180 => pose with { X = pose.X + inset / 2 },
            _ => pose with { Y = pose.Y + inset / 2 }
        };
    }

    // The local duck faces right: floor -> right wall -> ceiling -> left wall.
    public static EdgePose At(double distance, double width, double height)
    {
        width = Math.Max(0, width);
        height = Math.Max(0, height);
        double length = Length(width, height);
        if (length == 0)
        {
            return new(0, 0, 0);
        }

        distance = ((distance % length) + length) % length;
        if (distance < width)
        {
            return new(distance, height, 0);
        }

        distance -= width;
        if (distance < height)
        {
            return new(width, height - distance, 270);
        }

        distance -= height;
        if (distance < width)
        {
            return new(width - distance, 0, 180);
        }

        return new(0, distance - width, 90);
    }
}
