namespace DatabaseBatch.Infrastructure
{
    public enum CommandType
    {
        Add,
        Modify,
        Drop,
        Change,
        Alter,
        Create,

        Max
    }
    public enum ClassificationType
    {
        Column,
        Index,
        PrimaryKey,
        ForeignKey,

        Max
    }
}
