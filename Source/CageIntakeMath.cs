namespace BetterRimworlds.ChickenBatteryCage;

public static class CageIntakeMath
{
    public static bool AcceptsAge(long biologicalAgeTicks, float maximumYears, int ticksPerYear)
    {
        return maximumYears < 0f || biologicalAgeTicks <= (double)maximumYears * ticksPerYear;
    }
}
