using apbd_cw7_ado.net_s33982.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace apbd_cw7_ado.net_s33982.Controllers {
    [Route("api/[controller]")]
    [ApiController]
    public class AppointmentsController : ControllerBase {
        private readonly string connectionString;

        public AppointmentsController(IConfiguration configuration) {
            connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] AppointmentSearchDTO search) {
            var lista = new List<AppointmentListDTO>();
            try {
                await using (var connection = new SqlConnection(connectionString)) {
                    await connection.OpenAsync();

                     await using var command = new SqlCommand("""
                                                             SELECT
                                                                 a.IdAppointment,
                                                                 a.AppointmentDate,
                                                                 a.Status,
                                                                 a.Reason,
                                                                 p.FirstName + N' ' + p.LastName AS PatientFullName,
                                                                 p.Email AS PatientEmail
                                                             FROM Appointments a
                                                             JOIN Patients p ON p.IdPatient = a.IdPatient
                                                             WHERE (@Status IS NULL OR a.Status = @Status)
                                                               AND (@PatientLastName IS NULL OR p.LastName = @PatientLastName)
                                                             ORDER BY a.AppointmentDate;
                                                             """, connection);
                     command.Parameters.AddWithValue("@Status", search.status.Length > 0 ? search.status : DBNull.Value);
                     command.Parameters.AddWithValue("@PatientLastName", search.patientLastName.Length > 0 ? search.patientLastName : DBNull.Value);
                     
                     await using var reader = await command.ExecuteReaderAsync();

                     while (await reader.ReadAsync()) {
                         var id = reader.GetInt32(0);
                         var appDate = reader.GetDateTime(1);
                         var status = reader.GetString(2);
                         var reason = reader.GetString(3);
                         var patientFullName = reader.GetString(4);
                         var patientEmail = reader.GetString(5);
                         var temp = new AppointmentListDTO(id, appDate, status, reason, patientFullName, patientEmail);
                         lista.Add(temp);
                     }

                     if (lista.Count == 0) {
                         return NotFound();
                     }
                     
                    return Ok(lista);
                }
            }
            catch (Exception e) {
                return StatusCode(500, $"Błąd bazy danych: {e.Message}");
            }
        }
        
        [HttpGet]
        [Route("{id}")]
        public async Task<IActionResult> GetById([FromRoute] int id) {
            try {
                await using (var connection = new SqlConnection(connectionString)) {
                    await connection.OpenAsync();

                     await using var command = new SqlCommand("""
                                                             SELECT
                                                                 IdAppointment,
                                                                 IdPatient,
                                                                 IdDoctor,
                                                                 AppointmentDate,
                                                                 Status,
                                                                 Reason,
                                                                 InternalNotes
                                                             FROM Appointments WHERE IdAppointment = @IdAppointment
                                                             """, connection);
                     command.Parameters.AddWithValue("@IdAppointment", id);

                     await using var reader = await command.ExecuteReaderAsync();

                     while (await reader.ReadAsync()) {
                         
                         var idAppointment = reader.GetInt32(0);
                         var idPatient = reader.GetInt32(1);
                         var idDoctor = reader.GetInt32(2);
                         var appointmentDate = reader.GetDateTime(3);
                         var status = reader.GetString(4);
                         var reason = reader.GetString(5);
                         var internalNotes = reader.IsDBNull(6) ? null : reader.GetString(6);
                         return Ok(new AppointmentDetailsDTO(idAppointment, idPatient, idDoctor, appointmentDate, status, reason, internalNotes));
                     }
                     
                     return NotFound();
                         
                }
            }
            catch (Exception e) {
                return StatusCode(500, $"Błąd bazy danych: {e.Message}");
            }
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] CreateAppointmentRequestDTO createAppointmentRequestDto) {
            try {
                await using (var connection = new SqlConnection(connectionString)) {
                    await connection.OpenAsync();

                     await using var patientExists = new SqlCommand("""
                                                             SELECT
                                                                 IdPatient,
                                                                 IsActive
                                                             FROM Patients WHERE IdPatient = @IdPatient AND IsActive = 1
                                                             """, connection);
                     patientExists.Parameters.AddWithValue("@IdPatient", createAppointmentRequestDto.idPatient);

                     await using var readerPatient = await patientExists.ExecuteReaderAsync();
                     if (!await readerPatient.ReadAsync()) {
                         return Conflict("Pacjent nie istnieje");
                     }
                     
                     await using var doctorExists = new SqlCommand("""
                                                                    SELECT
                                                                        IdDoctor,
                                                                        IsActive
                                                                    FROM Doctors WHERE IdDoctor = @IdDoctor AND IsActive = 1
                                                                    """, connection);
                     doctorExists.Parameters.AddWithValue("@IdDoctor", createAppointmentRequestDto.idDoctor);

                     await using var readerDoctor = await patientExists.ExecuteReaderAsync();
                     if (!await readerDoctor.ReadAsync()) {
                         return Conflict("Doktor nie istnieje");
                     }

                     if (createAppointmentRequestDto.appointmentDate < DateTime.Now) {
                         return Conflict("Data w przeszłości");
                     }

                     if (createAppointmentRequestDto.reason == null || createAppointmentRequestDto.reason.Length == 0 || createAppointmentRequestDto.reason.Length > 250) {
                         return Conflict("Nie podano reason lub nie miesci sie w przedziale (0, 250]");
                     }

                     await using var insertCommand = new SqlCommand("INSERT INTO Appointments (IdPatient, IdDoctor, AppointmentDate, Status, Reason, InternalNotes) VALUES (@IdPatient, @IdDoctor, @AppointmentDate, 'Scheduled', @Reason, '')", connection);
                     insertCommand.Parameters.AddWithValue("@IdPatient", createAppointmentRequestDto.idPatient);
                     insertCommand.Parameters.AddWithValue("@IdDoctor", createAppointmentRequestDto.idDoctor);
                     insertCommand.Parameters.AddWithValue("@AppointmentDate", createAppointmentRequestDto.appointmentDate);
                     insertCommand.Parameters.AddWithValue("@Reason", createAppointmentRequestDto.reason);
                    

                     using var rowsInserted = insertCommand.ExecuteNonQueryAsync();
                     if (!await readerDoctor.ReadAsync()) {
                         return Conflict("Doktor nie istnieje");
                     }

                     return Created();

                }
            }
            catch (Exception e) {
                return StatusCode(500, $"Błąd bazy danych: {e.Message}");
            }
        }
    }
}