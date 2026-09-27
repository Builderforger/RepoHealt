using RepoHealth.Collection;

namespace RepoHealth.Scoring.Tests;

/// Токен попадает в адрес для клонирования — значит он может
/// попасть и в сообщение об ошибке, а оттуда в отчёт, который
/// пользователь кому-нибудь покажет. Проверяем оба конца.
public class GitAddressTests
{
    private const string Url = "https://git.sourcecraft.dev/org/project.git";
    private const string Token = "pv1_secret_value";

    [Fact]
    public void WithoutTokenAddressIsUnchanged()
    {
        Assert.Equal(Url, GitAddress.WithToken(Url, null));
        Assert.Equal(Url, GitAddress.WithToken(Url, ""));
        Assert.Equal(Url, GitAddress.WithToken(Url, "   "));
    }

    [Fact]
    public void WithTokenAddressPointsToSameRepository()
    {
        var withToken = GitAddress.WithToken(Url, Token);

        Assert.Contains(Token, withToken);
        Assert.StartsWith("https://git:", withToken);
        Assert.EndsWith("git.sourcecraft.dev/org/project.git", withToken);
    }

    [Fact]
    public void TokenIsNotSentOverPlainHttp()
    {
        // Иначе токен ушёл бы по сети в открытом виде.
        var insecure = "http://git.sourcecraft.dev/org/project.git";

        Assert.Equal(insecure, GitAddress.WithToken(insecure, Token));
    }

    [Fact]
    public void MalformedAddressDoesNotBreakCollection()
    {
        Assert.Equal("не адрес", GitAddress.WithToken("не адрес", Token));
    }

    [Fact]
    public void TokenIsStrippedFromErrorMessage()
    {
        var message = $"fatal: Authentication failed for 'https://git:{Token}@git.sourcecraft.dev/org/project.git/'";

        var clean = GitAddress.Hide(message, Token);

        Assert.DoesNotContain(Token, clean);
        Assert.Contains("***", clean);
        Assert.Contains("Authentication failed", clean);
    }

    [Fact]
    public void WithoutTokenMessageIsUntouched()
    {
        var message = "fatal: repository not found";

        Assert.Equal(message, GitAddress.Hide(message, null));
        Assert.Equal(message, GitAddress.Hide(message, ""));
    }
}
