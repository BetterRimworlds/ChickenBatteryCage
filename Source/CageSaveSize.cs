/*
 * This file is part of BetterRimworlds.ChickenBatteryCage, a Better Rimworlds Project.
 *
 * Copyright © 2026 Theodore R. Smith
 * Author: Theodore R. Smith <hopeseekr@gmail.com>
 *   GPG Fingerprint: D8EA 6E4D 5952 159D 7759  2BB4 EEB6 CE72 F441 EC41
 *   https://github.com/BetterRimworlds/BetterRimworlds.ChickenBatteryCage
 *
 * This file is licensed under the MIT License.
 */

namespace BetterRimworlds.ChickenBatteryCage;

/**
 * A derived model of how much savegame a caged flock costs.
 *
 * The whole point of virtualization is that a caged chicken is a handful of
 * numbers, not a Pawn graph. The per-record figure is computed from the exact
 * serialized field set in <see cref="CagedChickenFields"/>, not from a
 * hand-picked constant, so adding a persisted field grows the estimate and a
 * passing budget keeps describing the real record. Each field is costed at the
 * widest text its value can occupy, a deliberate over-estimate, so a passing
 * budget means the real save is comfortably smaller.
 */
public static class CageSaveSize
{
    /// XML characters around each element's value: the closing "/>", the next
    /// element's "</", and the trailing newline.
    const int ElementOverheadChars = 5;

    /// Bytes of XML for one persisted field: two name occurrences (open and
    /// close tag) plus the value and the surrounding punctuation.
    public static int ElementBytes(PersistedField field)
    {
        return field.Name.Length * 2 + field.MaxValueWidth + ElementOverheadChars;
    }

    /// Derived from the persisted field set, so it cannot silently diverge
    /// from what the record actually scribes.
    public static int EstimatedBytesPerRecord
    {
        get
        {
            int total = 0;
            foreach (PersistedField field in CagedChickenFields.All)
            {
                total += ElementBytes(field);
            }

            return total;
        }
    }

    /// Fixed cost of a cage's own persisted state, regardless of flock size.
    public const int EstimatedBytesPerCage = 512;

    public static long EstimatedBytesForRecords(int recordCount)
    {
        return recordCount <= 0 ? 0L : (long)recordCount * EstimatedBytesPerRecord;
    }

    public static long EstimatedBytes(int cageCount, int recordCount)
    {
        long cages = cageCount <= 0 ? 0L : (long)cageCount * EstimatedBytesPerCage;
        return cages + EstimatedBytesForRecords(recordCount);
    }
}
