using DatabaseBatch.Infrastructure;

namespace DatabaseBatch.Models
{
    public class Publish
    {
        public string PreDeployment { get; set; }

        public string Deployment { get; set; } = Consts.OutputScript;

        public string PostDeployment { get; set; }

    }
}
