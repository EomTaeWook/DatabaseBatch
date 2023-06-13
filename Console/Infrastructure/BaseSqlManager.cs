using DatabaseBatch.Infrastructure.Interface;
using DatabaseBatch.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace DatabaseBatch.Infrastructure
{
    public abstract class BaseSqlManager : ISqlManager
    {
        public abstract void Init(Config config);
        public abstract void MakeScript();
        public abstract void Publish();

        protected Config _config;

        
        
    }
}
