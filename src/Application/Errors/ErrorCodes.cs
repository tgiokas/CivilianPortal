namespace CitizenPortal.Application.Errors;

public static class ErrorCodes
{
    public static class PORTAL
    {
        public const string GenericUnexpected = "PORTAL-000";
        public const string ErrorInErrorCodes = "PORTAL-001";
        public const string UserNotFound = "PORTAL-002";
        public const string ApplicationNotFound = "PORTAL-003";
        public const string ApplicationCreatedFailed = "PORTAL-004";
        public const string FileUploadFailed = "PORTAL-005";
        public const string StorageServiceUnavailable = "PORTAL-006";
        public const string InvalidFileType = "PORTAL-007";
        public const string FileTooLarge = "PORTAL-008";
        public const string UnauthorizedAccess = "PORTAL-009";
        public const string UserAlreadyExists = "PORTAL-010";
        public const string UserCreateFailed = "PORTAL-011";
        public const string InvalidApplicationData = "PORTAL-012";
        public const string OutboxPublishFailed = "PORTAL-013";
        public const string StatusUpdateFailed = "PORTAL-014";
        public const string AuthenticationFailed = "PORTAL-015";
        public const string RefreshFailed = "PORTAL-016";
        public const string LogoutFailed = "PORTAL-017";
        public const string PdfGenerationFailed = "PORTAL-018";
    }
}
