static class PrivacyPolicyText
{
    public const string Version = "1.0";

    public const string Value = """
TSD SUPPORT AGENT — POLÍTICA DE PRIVACIDADE

Quando a opção "Ativar comunicação com a Central TSD" estiver habilitada, o Agent envia por HTTPS para a Central exibida no instalador informações técnicas necessárias ao suporte e monitoramento do equipamento.

Dados enviados:
• nome do equipamento;
• fabricante, modelo, número de série e processador;
• versão do Windows e do Agent;
• uso de CPU, memória e disco;
• tempo ligado;
• estado agregado do antivírus e firewall;
• contagem de erros dos logs System/Application;
• lista de softwares instalados: nome, versão, publicador e arquitetura;
• chave pública criptográfica usada para autenticar o equipamento.

O Agent NÃO coleta nem envia:
• conteúdo de arquivos ou documentos;
• senhas, cookies ou credenciais;
• teclas digitadas;
• capturas de tela;
• câmera ou microfone;
• histórico do navegador;
• conteúdo de e-mails ou mensagens;
• geolocalização.

A comunicação pode ser desativada no instalador. Em uma instalação nova com comunicação desativada, o Agent não realiza enrollment, não cria chave de autenticação e não envia dados à Central. Em uma instalação já cadastrada, a comunicação é interrompida, mas a identidade criptográfica local pode ser preservada para permitir reativação futura sem novo cadastro.

A desinstalação remove o serviço, o executável, os dados locais do Agent e sua chave criptográfica local, interrompendo novos envios. Registros já recebidos pela Central podem permanecer conforme a política de retenção definida pelo operador da Central.

Central padrão:
https://agent.toservicedesk.com.br

Política versão 1.0.
""";
}
