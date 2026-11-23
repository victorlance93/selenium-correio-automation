# language: pt

Funcionalidade: Busca CEP e rastreamento nos Correios

  Cenário: Consultar CEP inexistente, CEP válido e código inválido
    Dado que acesso a página Busca CEP dos Correios
    Quando pesquiso o CEP "80700000" e resolvo o CAPTCHA manualmente
    Então confirmo que o CEP não foi encontrado
    Quando clico em Nova Busca
    E pesquiso o CEP "01013-001" e resolvo o CAPTCHA manualmente
    Então confirmo o endereço da Rua Quinze de Novembro em São Paulo
    Quando clico em Nova Busca
    E consulto no rastreamento o código "SS987654321BR"
    Então confirmo que o código de rastreamento é inválido
