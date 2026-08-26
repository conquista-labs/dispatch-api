namespace Dispatch.Application;

// CadastrarConferente precisa criar Usuario + Conferente juntos, os dois ou nenhum — daí
// não dá pra cada repositório commitar sozinho (arriscaria salvar um e falhar o outro).
// Os métodos de escrita dos repositórios só marcam o estado (Adicionar/Remover); quem
// decide quando de fato gravar é o caso de uso, chamando SalvarAsync uma vez no final.
public interface IUnitOfWork
{
    Task SalvarAsync(CancellationToken cancellationToken);
}
