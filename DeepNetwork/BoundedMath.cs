namespace DeepNetwork
{
    public static class BoundedMath
    {
        private const double SmallNumberNegative = -1E+8;
        private const double SmallNumberPositive = 1E+8;

        public static double Exp(double d)
        {
            d = Math.Exp(d);

            if (d < SmallNumberNegative)
            {
                return -1E+8;
            }

            return !(d > SmallNumberPositive) ? d : SmallNumberPositive;
        }
    }
}
