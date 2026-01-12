namespace GestionProjects.Application.Common.Exceptions
{
    /// <summary>
    /// Exception métier pour les erreurs de logique métier
    /// </summary>
    public class BusinessException : Exception
    {
        public BusinessException(string message) : base(message)
        {
        }

        public BusinessException(string message, Exception innerException) 
            : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// Exception pour les erreurs de validation
    /// </summary>
    public class ValidationException : BusinessException
    {
        public ValidationException(string message) : base(message)
        {
        }

        public ValidationException(string message, Exception innerException) 
            : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// Exception pour les erreurs d'autorisation
    /// </summary>
    public class UnauthorizedBusinessException : BusinessException
    {
        public UnauthorizedBusinessException(string message) : base(message)
        {
        }
    }
}

