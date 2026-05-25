namespace CitizenPortal.Domain.Enums;

public enum ApplicationStatus
{
    Submitted = 0,       /// Citizen submitted, saved in CitizenPortal DB
    Delivered = 1,      /// DMS assigned protocol number
    Rejected = 2        /// DMS rejected the application
}
