using DatabaseBatch.Models;

namespace DatabaseBatch.Infrastructure.Interface
{
    public interface ISqlManager
    {
        void Init(Config config);
        void MakeScript();
        void Publish();
    }
}
