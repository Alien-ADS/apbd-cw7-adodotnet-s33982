namespace apbd_cw7_ado.net_s33982.DTOs;

public class AppointmentDetailsDTO {
    public int IdAppointment { get; set; }
    public int IdPatient { get; set; }
    public int IdDoctor { get; set; }
    public DateTime AppointmentDate { get; set; }
    public string Status { get; set; }
    public string Reason  { get; set; }
    public string InternalNotes  { get; set; }

    public AppointmentDetailsDTO(int idAppointment, int idPatient, int idDoctor, DateTime appointmentDate, string status, string reason, string internalNotes) {
        IdAppointment = idAppointment;
        IdPatient = idPatient;
        IdDoctor = idDoctor;
        AppointmentDate = appointmentDate;
        Status = status;
        Reason = reason;
        InternalNotes = internalNotes;
    }
}