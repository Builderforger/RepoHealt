namespace RepoHealth.Collection;

/// Адрес для клонирования и всё, что связано с токеном в нём.
///
/// Вынесено отдельно, потому что здесь легко ошибиться дважды:
/// не подставить токен — и не прочитать закрытый репозиторий;
/// подставить, но не вычистить из сообщений — и токен окажется
/// в отчёте, который пользователь потом кому-нибудь покажет.
public static class GitAddress
{
    /// Подставляет токен в адрес. Без токена адрес не меняется.
    public static string WithToken(string cloneUrl, string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return cloneUrl;

        if (!Uri.TryCreate(cloneUrl, UriKind.Absolute, out var address))
            return cloneUrl;

        if (address.Scheme != Uri.UriSchemeHttps)
            return cloneUrl;

        // Имя пользователя SourceCraft не проверяет —
        // важен пароль, которым и служит токен.
        return $"{address.Scheme}://git:{token}@{address.Authority}{address.PathAndQuery}";
    }

    /// Убирает токен из текста. Git охотно повторяет адрес
    /// в сообщениях об ошибках, а в адресе лежит токен.
    public static string Hide(string text, string? token)
    {
        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(text))
            return text;

        return text.Replace(token, "***");
    }
}
