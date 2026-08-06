using System.Collections.Generic;

namespace GooGalaxy.Runtime.Shared.Types
{
    /// <summary>
    /// What one landing did to the units around it: the units whose ownership flipped, and the armored units
    /// that absorbed the attempt instead. Lives in Shared so the conversion event can cross assembly
    /// boundaries without any consumer depending on the Board feature assembly.
    /// </summary>
    /// <remarks>
    /// Both lists are owned by the publisher and are only valid for the duration of the callback: they are
    /// reusable buffers that the next landing clears and refills. Subscribers must copy what they intend to
    /// keep, and must read them with an indexed <c>for</c> loop — <c>foreach</c> over the interface boxes the
    /// backing enumerator, one allocation per subscriber per landing.
    /// A unit appears in at most one of the two lists, and at most once overall: per the GDD conversion rules
    /// a single landing never both strips armor from and converts the same piece.
    /// Equality is deliberately not implemented. The struct wraps borrowed buffers rather than values, so
    /// reference equality would report two unrelated landings as equal whenever they reuse the same buffers,
    /// and a content comparison would be an unbounded allocation-free-path cost for no caller that needs it.
    /// </remarks>
    public readonly struct ConversionResult
    {
        /// <summary>Wraps the two id buffers a landing filled. Either may be null, which reads as empty.</summary>
        /// <param name="convertedUnitIds">The units whose ownership flipped.</param>
        /// <param name="armorStrippedUnitIds">The armored units that spent their armor instead.</param>
        public ConversionResult(IReadOnlyList<int> convertedUnitIds, IReadOnlyList<int> armorStrippedUnitIds)
        {
            ConvertedUnitIds = convertedUnitIds;
            ArmorStrippedUnitIds = armorStrippedUnitIds;
        }

        /// <summary>Identifiers of the units whose ownership flipped to the acting player.</summary>
        public IReadOnlyList<int> ConvertedUnitIds { get; }

        /// <summary>
        /// Identifiers of the armored units that spent their armor absorbing the attempt. Their ownership is
        /// unchanged, and a second, separate landing is required to flip them.
        /// </summary>
        public IReadOnlyList<int> ArmorStrippedUnitIds { get; }

        /// <summary>Whether the landing changed nothing, including on a default-constructed value.</summary>
        public bool IsEmpty => GetCount(ConvertedUnitIds) == 0 && GetCount(ArmorStrippedUnitIds) == 0;

        private static int GetCount(IReadOnlyList<int> unitIds)
        {
            return unitIds == null ? 0 : unitIds.Count;
        }
    }
}
