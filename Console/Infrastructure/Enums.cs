namespace DatabaseBatch.Infrastructure
{
    public enum AlterTableType
    {
        Add,
        Modify,
        Drop,

        Max
    }
    public enum PublishDeploymentType
    {
        PreDeployment,
        Deployment,
        PostDeployment,

        Max
    }

}
