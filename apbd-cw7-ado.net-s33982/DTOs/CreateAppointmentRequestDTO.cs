namespace apbd_cw7_ado.net_s33982.DTOs;

public class CreateAppointmentRequestDTO {
    public int idPatient { get; set; }
    public int idDoctor { get; set; }
    public DateTime appointmentDate { get; set; }
    public string reason { get; set; }
}