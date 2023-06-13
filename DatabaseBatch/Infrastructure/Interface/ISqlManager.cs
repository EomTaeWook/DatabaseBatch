using DatabaseBatch.Models;

namespace DatabaseBatch.Infrastructure.Interface
{
    public interface ISqlManager
    {
        void MakeScript();
        void Publish();
    }
}
