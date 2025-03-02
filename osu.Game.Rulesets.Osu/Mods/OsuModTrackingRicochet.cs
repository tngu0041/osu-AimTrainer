// Tracking movement.

using System;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Scoring;
using osuTK;

namespace osu.Game.Rulesets.Osu.Mods
{
    internal class OsuModTrackingRicochet : ModWithVisibilityAdjustment
    {
        public override string Name => "Tracking Ricochet";
        public override string Acronym => "TR";
        public override IconUsage? Icon => FontAwesome.Solid.ArrowCircleLeft;
        public override ModType Type => ModType.Fun;
        public override LocalisableString Description => "Hit objects move and bounce off walls!";
        public override double ScoreMultiplier => 1;
        public override Type[] IncompatibleMods => new[] { typeof(OsuModTransform), typeof(OsuModMagnetised), typeof(OsuModRepel), typeof(OsuModDepth) };
        private const float base_speed = 10f; // Base strafing speed (higher = faster movement)

        [SettingSource("Speed", "Multiplier applied to the speed.")]
        public BindableDouble Speed { get; } = new BindableDouble(1)
        {
            MinValue = 0.1f,
            MaxValue = 4f,
            Precision = 0.1f
        };

        [SettingSource("Random", "Randomize the initial movement direction.")]
        public BindableBool Random { get; } = new BindableBool(false);

        private int seed = new Random().Next(4096);

        protected override void ApplyIncreasedVisibilityState(DrawableHitObject hitObject, ArmedState state) => drawableOnApplyCustomUpdateState(hitObject, state);

        protected override void ApplyNormalVisibilityState(DrawableHitObject hitObject, ArmedState state) => drawableOnApplyCustomUpdateState(hitObject, state);

        private (Vector2 offset, float travelDistance, int wallIndex) getRandomExitOffset(Vector2 spawnPoint, double angle)
        {
            float x = spawnPoint.X;
            float y = spawnPoint.Y;

            const float playfield_width = 512;
            const float playfield_height = 384;
            const float epsilon = 1e-5f;

            float cosA = (float)Math.Cos(angle);
            float sinA = (float)Math.Sin(angle);

            // Ensure cosA and sinA are never exactly zero
            cosA = (cosA == 0) ? epsilon : cosA;
            sinA = (sinA == 0) ? epsilon : sinA;

            // Compute distances to each wall
            float tMaxX = (cosA < 0) ? (x / -cosA) : ((playfield_width - x) / cosA);
            float tMaxY = (sinA > 0) ? ((playfield_height - y) / sinA) : (y / -sinA);

            // Choose the shortest distance
            float t = Math.Min(tMaxX, tMaxY);

            // Compute exit point
            float exitX = x + t * cosA;
            float exitY = y + t * sinA;

            // Clamp exit point within bounds
            exitX = Math.Clamp(exitX, 0, playfield_width);
            exitY = Math.Clamp(exitY, 0, playfield_height);

            // Compute offset vector
            Vector2 offset = new Vector2(exitX - x, exitY - y);

            // Determine which wall is hit (fixed for osu! flipped y-axis)
            int wallIndex;
            if (t == tMaxX)
                wallIndex = (cosA < 0) ? 0 : 1; // Left (0) or Right (1)
            else
                wallIndex = (sinA > 0) ? 3 : 2; // Bottom (3) or Top (2)

            return (offset, t, wallIndex);
        }

        public static double ReflectAngle(double angleRad, int wall)
        {
            // Normalize the angle to [0, 2π)
            angleRad = (angleRad % (2 * Math.PI) + 2 * Math.PI) % (2 * Math.PI);

            switch (wall)
            {
                case 0: // Left wall
                case 1: // Right wall
                    angleRad = Math.PI - angleRad;
                    break;

                case 2: // Top wall
                case 3: // Bottom wall
                    angleRad = -angleRad;
                    break;

                default:
                    throw new ArgumentException("Invalid wall index. Must be 0 (left), 1 (right), 2 (top), or 3 (bottom).");
            }

            // Normalize to [0, 2π)
            return (angleRad % (2 * Math.PI) + 2 * Math.PI) % (2 * Math.PI);
        }

        public static double GenerateValidAngle(Random objRand, Vector2 origin)
        {
            double x = origin.X;
            double y = origin.Y;
            double minAngle = 0;
            double maxAngle = 2 * Math.PI;

            // Handle wall collisions (avoiding illegal directions based on position)

            // Left wall (x <= 1) can only go right (0 to π/2)
            if (x <= 1) { minAngle = -Math.PI / 2; maxAngle = Math.PI / 2; }

            // Right wall (x >= 511) can only go left (π/2 to 3π/2)
            else if (x >= 511) { minAngle = Math.PI / 2; maxAngle = 3 * Math.PI / 2; }

            // Top wall (y <= 1) can only go down (0 to π)
            if (y <= 1) { minAngle = Math.Max(minAngle, 0); maxAngle = Math.Min(maxAngle, Math.PI); }

            // Bottom wall (y >= 383) can only go up (π to 2π)
            else if (y >= 383) { minAngle = Math.Max(minAngle, Math.PI); maxAngle = Math.Min(maxAngle, 2 * Math.PI); }

            // Handle corner cases
            // Top-left corner (x <= 1, y <= 1) can only go down-right (0 to π/2)
            if (x <= 1 && y <= 1) { minAngle = 0; maxAngle = Math.PI / 2; }

            // Bottom-left corner (x <= 1, y >= 383) can only go up-right (-π/2 to 0)
            else if (x <= 1 && y >= 383) { minAngle = -Math.PI / 2; maxAngle = 0; }

            // Top-right corner (x >= 511, y <= 1) can only go down-left (π/2 to π)
            else if (x >= 511 && y <= 1) { minAngle = Math.PI / 2; maxAngle = Math.PI; }

            // Bottom-right corner (x >= 511, y >= 383) can only go up-left (π to 3π/2)
            else if (x >= 511 && y >= 383) { minAngle = Math.PI; maxAngle = 3 * Math.PI / 2; }

            // Generate a random valid angle in the allowed range and normalize it between 0 and 2π
            double angle = objRand.NextDouble() * (maxAngle - minAngle) + minAngle;
            return (angle + 2 * Math.PI) % (2 * Math.PI);  // Normalize between 0 and 2π
        }

        private void drawableOnApplyCustomUpdateState(DrawableHitObject drawable, ArmedState state)
        {
            var osuObject = (OsuHitObject)drawable.HitObject;
            Vector2 origin = drawable.Position;
            Random objRand = Random.Value ? new Random((int)osuObject.StartTime + seed) : new Random((int)osuObject.StartTime);

            // Ignore repeat points and tails and spinners
            if (osuObject is SliderRepeat || osuObject is SliderTailCircle || osuObject is Spinner)
                return;

            if ((osuObject is HitCircle && !(osuObject is SliderHeadCircle)) || osuObject is IHasDuration)
            {
                // Calculate object's max duration
                double maxDuration = osuObject is IHasDuration ? osuObject.GetEndTime() - osuObject.StartTime + osuObject.TimePreempt : osuObject.TimePreempt + osuObject.HitWindows.WindowFor(HitResult.Miss);
                double elapsedTime = 0;
                double angle = GenerateValidAngle(objRand, origin);

                while (elapsedTime < maxDuration)
                {
                    (Vector2 moveOffset, float travelDist, int wallIndex) = getRandomExitOffset(origin, angle);
                    float moveDuration = (float)(travelDist * base_speed / Speed.Value);

                    using (drawable.BeginAbsoluteSequence(osuObject.StartTime - osuObject.TimePreempt + elapsedTime))
                    {
                        drawable.MoveTo(new Vector2(origin.X + moveOffset.X, origin.Y + moveOffset.Y), moveDuration);
                    }
                    elapsedTime += moveDuration;
                    angle = ReflectAngle(angle, wallIndex);
                }
            }
        }
    }
}
