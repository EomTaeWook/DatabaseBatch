namespace DatabaseBatch.Infrastructure
{
    public enum CommandType
    {
        Add,
        Modify,
        Drop,
        Change, 
        Alter,

        Max
    }
    public enum ClassificationType
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
