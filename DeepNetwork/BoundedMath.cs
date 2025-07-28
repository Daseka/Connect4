namespace DeepNetwork
{
    public static class BoundedMath
    {
        private const double SmallNumberNegative = -1E+8;
        private const double SmallNumberPositive = 1E+8;

        public static double Exp(double d)
        {
            d = Math.Exp(d);

            return d < SmallNumberNegative 
                ? -1E+8 
                : !(d > SmallNumberPositive) ? d : SmallNumberPositive;
        }
    }
}
