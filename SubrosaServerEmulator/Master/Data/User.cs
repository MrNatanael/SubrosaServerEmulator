namespace SubrosaServerEmulator.Master.Data;

public class User
{
    public string Username { get; set; } = string.Empty;
    // TODO: I'm actually not sure what these are used for!
    public int RegistrationId { get; set; } 
    public int RegistrationSeq { get; set; }
}