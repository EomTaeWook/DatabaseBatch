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
    }
}
