namespace Arise_RTM
{
    internal static class JenkinsHash
    {
        public static uint Joaat(string text)
        {
            unchecked
            {
                uint hash = 0;
                foreach (char c in text.ToLowerInvariant())
                {
                    hash += (byte)c;
                    hash += hash << 10;
                    hash ^= hash >> 6;
                }

                hash += hash << 3;
                hash ^= hash >> 11;
                hash += hash << 15;
                return hash;
            }
        }
    }
}
