namespace Chisel.Core.Settings;

public interface IUserSettingsRepository
{
    UserSettings? Get();
    void Save(UserSettings settings);
}
