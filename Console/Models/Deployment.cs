namespace DatabaseBatch.Models
{
    public class Deployment
    {
        public string PreDeploymentSql { get; set; }

        public string DeploymentSql { get; set; }

        public string PostDeploymentSql { get; set; }

    }
}
