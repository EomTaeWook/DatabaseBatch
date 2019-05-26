using System;

namespace DatabaseBatch.Infrastructure
{
    public class Singleton<T> where T : class
    {
        private static Lazy<T> _instance = new Lazy<T>();
        public static T Instance => _instance.Value;
    }
}
