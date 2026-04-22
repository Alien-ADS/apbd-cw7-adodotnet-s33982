namespace apbd_cw7_ado.net_s33982.DTOs;

public class AppointmentListDTO {
    public int IdAppointment { get; set; }
    public DateTime AppointmentDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string PatientFullName { get; set; } = string.Empty;
    public string PatientEmail { get; set; } = string.Empty;
    
    public AppointmentListDTO() {}

    public AppointmentListDTO(int idAppointment, DateTime appointmentDate, string status, string reason, string patientFullName, string patientEmail) {
        IdAppointment  = idAppointment;
        AppointmentDate = appointmentDate;
        Status = status;
        Reason = reason;
        PatientFullName = patientFullName;
        PatientEmail = patientEmail;
    }
}