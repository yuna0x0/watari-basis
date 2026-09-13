using System;
using System.Collections.Generic;
using yuna0x0.Basis.Convert.Model;

namespace yuna0x0.Basis.Convert.Mapping
{
    /// <summary>
    /// Which of Basis's visemes a VRM expression stands for.
    /// <para>
    /// Basis takes fifteen, in the order `AvatarSDKVisemes` in the Basis SDK lists them: sil, PP,
    /// FF, TH, DD, kk, CH, SS, nn, RR, aa, E, ih, oh, ou. VRM names five vowels and no
    /// consonants, so five slots can be filled from an avatar's own expressions and the other ten
    /// are left unset.
    /// </para>
    /// </summary>
    public static class VrmExpressionToVisemeMapper
    {
        public const int VisemeCount = 15;

        /// <summary>
        /// The five vowels, under both formats' names for them: VRM 1.0 writes `Aa`, `Ih`, `Ou`,
        /// `Ee` and `Oh` as preset fields, VRM 0.x writes `A`, `I`, `U`, `E` and `O` as clip
        /// names.
        /// </summary>
        private static readonly Dictionary<string, int> Slots =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                { "aa", 10 }, { "a", 10 },
                { "ee", 11 }, { "e", 11 },
                { "ih", 12 }, { "i", 12 },
                { "oh", 13 }, { "o", 13 },
                { "ou", 14 }, { "u", 14 },
            };

        /// <summary>The Basis viseme this expression fills, if it is one of the five vowels.</summary>
        public static bool TryGetSlot(VrmExpressionData expression, out int slot)
        {
            slot = -1;
            if (expression == null || expression.Role != VrmExpressionRole.Viseme)
            {
                return false;
            }

            // A 0.x clip is identified by its preset; its name is whatever the author typed.
            string key = string.IsNullOrEmpty(expression.PresetName)
                ? expression.Name
                : expression.PresetName;
            return !string.IsNullOrEmpty(key) && Slots.TryGetValue(key.Trim(), out slot);
        }

        /// <summary>
        /// Whether this is the expression that closes both eyes. Basis has one blink slot, so the
        /// one-eyed expressions, `BlinkLeft` and `Blink_R` and their kind, are not it.
        /// </summary>
        public static bool IsBlink(VrmExpressionData expression)
        {
            if (expression == null || expression.Role != VrmExpressionRole.Blink)
            {
                return false;
            }

            string key = string.IsNullOrEmpty(expression.PresetName)
                ? expression.Name
                : expression.PresetName;
            return string.Equals(key?.Trim(), "blink", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The one blendshape an expression drives, or null. A viseme slot holds a single shape,
        /// so an expression that moves several at once, as a jaw and a lip together, cannot be
        /// written as one and is reported instead.
        /// </summary>
        public static VrmMorphBinding SingleBinding(VrmExpressionData expression)
        {
            return expression != null && expression.Bindings.Count == 1
                   && !string.IsNullOrEmpty(expression.Bindings[0].ShapeName)
                ? expression.Bindings[0]
                : null;
        }
    }
}
