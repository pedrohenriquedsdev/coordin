# Spec Delta

## ADDED Requirements

### Requirement: Botão de exportação no cronograma
O cronograma SHALL mostrar o botão "Exportar planilha" acima da lista, visível para quem tem sessão, e o botão SHALL ficar desabilitado enquanto o download está em andamento (RF-19). O download usa o endpoint de exportação do cronograma (schedule-export).

#### Scenario: Botão visível
- **WHEN** o cronograma está carregado
- **THEN** o botão "Exportar planilha" aparece e está habilitado

#### Scenario: Download em andamento
- **WHEN** o gestor aciona o botão e o arquivo ainda está sendo baixado
- **THEN** o botão fica desabilitado até o fim do download
