using CitizenPortal.Domain.Enums;

namespace CitizenPortal.Domain.Entities;

/// Audit trail of every call to the GSIS authentication services (TaxisNet and
/// «Κωδικοί Δημόσιας Διοίκησης»), required by the Ministry of Digital Governance
/// "Πολιτική Ορθής Χρήσης Διαδικτυακών Υπηρεσιών" so that each authentication
/// attempt is traceable and auditable.
public class AuthenticationAuditLog
{
    /// Fixed reason for every authentication call in this portal.
    public const string DefaultReason = "Αυθεντικοποίηση χρήστη για υποβολή αίτησης";

    public long Id { get; set; }                                 // Μοναδικός αύξων αριθμός κλήσης
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;   // Ημερομηνία/ώρα της κλήσης (UTC)
    public AuthenticationProvider Provider { get; set; }         // Υπηρεσία αυθεντικοποίησης
    public string Reason { get; set; } = DefaultReason;          // Αιτία κλήσης
    public string? IpAddress { get; set; }                       // IP σταθμού εργασίας / εξυπηρετητή
    public string? Username { get; set; }                        // username φυσικού χρήστη
    public string? MachineName { get; set; }                     // machine name, όπου διαθέσιμο
    public bool Success { get; set; }                            // Αποτέλεσμα: Επιτυχής / Αποτυχημένη
    public string? FailureReason { get; set; }                   // λεπτομέρεια αποτυχίας (ιχνηλασιμότητα)
    public Guid? KeycloakUserId { get; set; }                    // συσχέτιση, όπου διαθέσιμο
}
