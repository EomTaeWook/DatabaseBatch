using System.Text;

namespace DatabaseBatch.Extensions
{
    public static class StringExtension
    {
        public static string Replace(this string source, string[] replacements, string toReplace)
        {
            var sb = new StringBuilder(source);
            foreach(var word in replacements)
            {
                sb.Replace(word, toReplace);
            }
            return sb.ToString();
        }
        public static int IndexOfAny(this string source, string[] anyOf, int startIndex, out int nextIndex)
        {
            int index = -1;
            nextIndex = 0;
            foreach(var value in anyOf)
            {
                var findIndex = source.IndexOf(value, startIndex);
                if (index == -1)
                {
                    index = findIndex;
                    nextIndex = value.Length;
                }
                else if (findIndex < index && findIndex != -1)
                {
                    index = findIndex;
                    nextIndex = value.Length;
                }
            }
            return index;
        }
    }
}
