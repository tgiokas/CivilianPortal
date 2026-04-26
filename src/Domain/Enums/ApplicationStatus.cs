namespace CitizenPortal.Domain.Enums;

public enum ApplicationStatus
{
    Submitted = 0,       /// Citizen submitted, saved in CitizenPortal DB
    Processing = 1,      /// DMS picked up the event, is processing
    Registered = 2,      /// DMS assigned protocol number
    Rejected = 3,        /// DMS rejected the application
    Completed = 4        /// Fully processed and closed
}
