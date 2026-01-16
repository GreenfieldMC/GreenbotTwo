namespace GreenbotTwo.Services.Interfaces;

public interface IServerWebConsole
{

    Task<Result<string>> SendCommand(string command);

}