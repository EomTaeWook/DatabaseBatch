namespace DatabaseBatch.Infrastructure
{
    public enum AlterTableType
    {
        Add,
        Modify,
        Drop,

        Max
    }
    public enum ChangeType
    {
        Columns,
        Index,
        PrimaryKey,
        ForeignKey,

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
