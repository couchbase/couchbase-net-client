using System;

namespace Couchbase.LoadTests.Helpers
{
    public static class RandomExtensions
    {
        private static readonly char[] AlphanumericCharacters = new[]
        {
            'A','B','C','D','E','F','G','H','I','J','K','L','M','N','O','P','Q','R','S','T','U','V','W','X','Y','Z',
            'a','b','c','d','e','f','g','h','i','j','k','l','m','n','o','p','q','r','s','t','u','v','w','x','y','z',
            '0','1','2','3','4','5','6','7','8','9'
        };

        public static unsafe string GetAlphanumericString(this Random random, int length)
        {
            if (length <= 0)
            {
                return "";
            }

            var charArrayLength = AlphanumericCharacters.Length;

            var result = new string(' ', length);
            fixed (char* dest = result)
            {
                for (var i = 0; i < length; i++)
                {
                    *(dest + i) = AlphanumericCharacters[random.Next() % charArrayLength];
                }
            }

            return result;
        }
    }
}
