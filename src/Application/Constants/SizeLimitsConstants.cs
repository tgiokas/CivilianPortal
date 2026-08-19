namespace CitizenPortal.Application.Constants;

// Kestrel's ceiling is kept above
// <see cref="MaxUploadBytes"/> so FileSizeLimitFilter always trips first with the
// app's own Result{T} error contract, instead of Kestrel aborting the read.
public static class SizeLimitsConstants
{
    public const long MaxAttachmentBytes = 50L * 1024 * 1024; // 50 MB — matches Kestrel MaxRequestBodySize

    public const long MaxTotalAttachmentBytes = 50L * 1024 * 1024; // 50 MB aggregate cap per submission

    public const int MaxPdfBodyText = 2000; // 2000 chars max for PDF body text

    public const int MaxSubjectLength = 500; // matches Applications.Subject column

    public const int MaxEmailLength = 320;   // matches Applications.Email column

    public const int MaxAttachmentCount = 10;

    public const long MaxUploadBytes = 45L * 1024 * 1024; // 45 MB — app-level limit (per file / FileSizeLimitFilter)

    public const long KestrelMaxRequestBodySize = MaxUploadBytes + 5L * 1024 * 1024; // 50 MB — 5 MB buffer above MaxUploadBytes
}
