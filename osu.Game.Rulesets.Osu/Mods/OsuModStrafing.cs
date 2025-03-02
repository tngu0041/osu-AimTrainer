// A D strafing like an FPS game!

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
    internal class OsuModStrafing : ModWithVisibilityAdjustment
    {
        public override string Name => "Strafing";
        public override string Acronym => "SF";
        public override IconUsage? Icon => FontAwesome.Solid.ArrowsAltH;
        public override ModType Type => ModType.Fun;
        public override LocalisableString Description => "A D strafing like an FPS game!";
        public override double ScoreMultiplier => 1;
        public override Type[] IncompatibleMods => new[] { typeof(OsuModTransform), typeof(OsuModMagnetised), typeof(OsuModRepel), typeof(OsuModDepth) };
        private const float base_speed = 50f; // Base strafing speed (higher = faster movement)

        [SettingSource("Speed", "Multiplier applied to the strafing speed.")]
        public BindableDouble Speed { get; } = new BindableDouble(1)
        {
            MinValue = 0.1f,
            MaxValue = 3f,
            Precision = 0.1f
        };

        [SettingSource("Frequency", "How often direction changes occur.")]
        public BindableDouble Frequency { get; } = new BindableDouble(1)
        {
            MinValue = 0.1f,
            MaxValue = 3f,
            Precision = 0.1f
        };

        [SettingSource("Smooth Movement", "Smooth out the direction changes.")]

        public BindableBool SmoothMovement { get; } = new BindableBool(false);
        protected override void ApplyIncreasedVisibilityState(DrawableHitObject hitObject, ArmedState state) => drawableOnApplyCustomUpdateState(hitObject, state);

        protected override void ApplyNormalVisibilityState(DrawableHitObject hitObject, ArmedState state) => drawableOnApplyCustomUpdateState(hitObject, state);

        private void drawableOnApplyCustomUpdateState(DrawableHitObject drawable, ArmedState state)
        {
            var osuObject = (OsuHitObject)drawable.HitObject;
            Vector2 origin = drawable.Position;

            Random objRand = new Random((int)osuObject.StartTime);

            // Ignore repeat points and tails
            if (osuObject is SliderRepeat || osuObject is SliderTailCircle)
                return;

            if ((osuObject is HitCircle && !(osuObject is SliderHeadCircle)) || osuObject is IHasDuration)
            {
                // Calculate object's max duration
                double maxDuration = osuObject is IHasDuration ? osuObject.GetEndTime() - osuObject.StartTime + osuObject.TimePreempt : osuObject.TimePreempt + osuObject.HitWindows.WindowFor(HitResult.Miss);
                double elapsedTime = 0;
                int direction = objRand.Next(2) == 0 ? -1 : 1;

                while (elapsedTime < maxDuration)
                {
                    // Generate random duration between 0.5-2.0
                    double randomDuration = 1000 * (objRand.NextDouble() * (1 - 0.25) + 0.25) / Frequency.Value;
                    double distanceToMove = direction * base_speed * (float)Speed.Value * randomDuration / 1000;

                    // Move the hit circle
                    using (drawable.BeginAbsoluteSequence(osuObject.StartTime - osuObject.TimePreempt + elapsedTime))
                        if (SmoothMovement.Value)
                        {
                            drawable.MoveTo(new Vector2((float)(origin.X + distanceToMove), origin.Y), (float)(randomDuration), Easing.InOutSine);
                        }
                        else
                        {
                            drawable.MoveTo(new Vector2((float)(origin.X + distanceToMove), origin.Y), (float)(randomDuration));
                        }
                    elapsedTime += randomDuration;
                    direction = -direction;
                }
            }
        }
    }
}
