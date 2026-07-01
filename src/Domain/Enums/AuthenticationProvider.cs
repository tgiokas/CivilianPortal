namespace CitizenPortal.Domain.Enums;

public enum AuthenticationProvider
{
    Unknown = 0,               /// Provider could not be determined (e.g. token exchange failed)
    TaxisNet = 1,              /// gsis-taxis-*   (TaxisNet)
    PublicAdministration = 2   /// gsis-govuser-* (Κωδικοί Δημόσιας Διοίκησης)
}
