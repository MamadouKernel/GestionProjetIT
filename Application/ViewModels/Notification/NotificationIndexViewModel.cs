namespace GestionProjects.Application.ViewModels.Notification
{
    public class NotificationIndexViewModel
    {
        public List<GestionProjects.Domain.Models.Notification> Items { get; set; } = new();
        public int NonLues { get; set; }

        // Pagination
        public int PageNumber { get; set; }
        public int TotalPages { get; set; }
        public int TotalCount { get; set; }
        public int PageSize { get; set; }
    }
}
