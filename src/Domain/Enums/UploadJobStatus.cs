namespace CitizenPortal.Domain.Enums;

public enum UploadJobStatus
{
    Pending = 0,    /// Submitted, event published to Kafka, awaiting ARCHIUM's result
    Completed = 1,  /// ARCHIUM confirmed archiving (and protocol assignment, if requested)
    Failed = 2      /// ARCHIUM rejected the upload/archive request
}
