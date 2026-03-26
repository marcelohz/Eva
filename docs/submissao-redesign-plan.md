# Redesign de Submissao para Empresa, Veiculo e Motorista

## Objetivo

Redesenhar o fluxo de aprovacao de `empresa`, `veiculo` e `motorista` para que:

- o estado aprovado atual continue vivendo nas tabelas live
- a empresa edite um pacote separado, versionado por tentativa
- documentos e dados propostos pertençam explicitamente a uma submissao
- a fila de analistas seja alimentada apenas por envio explicito
- documentos possam ser aprovados/rejeitados individualmente
- documentos ja aprovados possam ser carregados para tentativas futuras sem nova revisao
- o historico fique claro para empresa e analistas

## Fora de escopo

- `viagem` nao entra nesse fluxo
- `viagens` nao sao revisadas por analistas
- nao ha necessidade de migrar historico antigo
- o sistema ainda esta em desenvolvimento, nao em producao

## Estado atual resumido

Hoje o sistema mistura:

- dados propostos em `fluxo_pendencia.dados_propostos`
- documentos ligados diretamente a entidade
- associacao tardia de documentos a um fluxo via `documento.fluxo_pendencia_id`
- calculo de prontidao por "documento mais recente por tipo"
- entrada automatica na fila ao salvar/upload/deletar

Isso causa ambiguidade sobre:

- a qual tentativa cada documento pertence
- se um reenvio reabre automaticamente ou nao
- se documentos antigos rejeitados ainda contam
- como exibir revisao por documento

## Direcao geral aprovada

### Entidades cobertas

- `empresa`
- `veiculo`
- `motorista`

### Principio central

Cada entidade tem:

- um estado live aprovado atual
- zero ou uma submissao em edicao
- varias submissoes historicas imutaveis

Uma submissao contem:

- os dados propostos do formulario
- os documentos incluidos naquela tentativa

Nada entra na fila automaticamente.
Somente o botao `Enviar para analise` coloca a submissao na fila.

## Decisoes fechadas

### 1. Viagem nao participa do fluxo

Confirmado:

- `viagem` ficou fora por engano na proposta inicial
- `viagem` nao tera submissao
- `viagem` nao entra em revisao de analista

### 2. Uma nova tentativa gera uma nova submissao

Confirmado:

- apos rejeicao, a submissao antiga nao sera reaberta
- ela permanece imutavel como historico
- quando a empresa voltar a editar, criamos uma nova submissao em `EM_EDICAO`
- a nova submissao aponta para a anterior via `submissao_origem_id`

Motivo:

- preserva exatamente o que foi rejeitado
- evita perder o snapshot da tentativa rejeitada
- simplifica auditoria

### 3. Existe no maximo uma submissao em edicao por entidade

Confirmado:

- uma entidade nao pode ter varios drafts paralelos
- o fluxo permitido e:
  - uma submissao em `EM_EDICAO`
  - ou nenhuma, caso nao haja alteracao em andamento

### 4. O draft ja e uma submissao

Correcao aprovada:

- nao existe um "pre-draft" separado da submissao
- quando a empresa altera algo e ainda nao ha draft, criamos uma `submissao` em `EM_EDICAO`
- salvar atualiza essa submissao
- enviar para analise apenas muda o status da mesma submissao para `AGUARDANDO_ANALISE`

### 5. Dados do formulario sao um item de revisao, como documentos

Confirmado:

- os dados propostos nao terao um parecer "geral" apenas
- os dados do formulario serao tratados como outro item submetido para aprovacao
- cada documento tera sua revisao individual
- os dados do formulario tambem terao revisao individual
- a aprovacao final da submissao sera derivada dos itens

Conclusao aprovada:

- nao usar colunas `*_dados` diretamente em `submissao`
- criar uma tabela separada `submissao_dados`

### 6. Documentos podem ser aprovados/rejeitados individualmente

Confirmado:

- cada documento tera status individual
- cada documento rejeitado tera seu proprio motivo de rejeicao
- isso deve ficar claro para analistas e empresa

### 7. Motivo individual por documento rejeitado

Confirmado:

- cada linha rejeitada em `submissao_documento` deve ter `motivo_rejeicao`
- se o documento estiver `REJEITADO`, o motivo e obrigatorio

### 8. Dados rejeitados tambem terao motivo proprio

Confirmado por simetria:

- `submissao_dados` tera `motivo_rejeicao`
- se os dados estiverem `REJEITADO`, o motivo e obrigatorio

### 9. Pode haver submissao so de documentos

Confirmado:

- se nenhum dado mudar, mas houver alteracao de documentos, a submissao ainda e valida
- nesse caso, os dados do formulario podem continuar equivalentes ao estado live

### 10. Dados ja aprovados e inalterados nao voltam para revisao

Refinamento aprovado:

- se os dados da nova submissao forem identicos ao estado live aprovado atual, nao precisam de nova revisao
- a linha de `submissao_dados` pode nascer como:
  - `status_revisao = APROVADO`
  - `carregado_do_live = true`

Opcao aprovada:

- usar `status_revisao = APROVADO`
- com flag `carregado_do_live = true`
- sem criar um status especial do tipo `APROVADO_CARREGADO`

### 11. Documentos ja aprovados podem ser carregados para nova tentativa

Confirmado:

- documentos ja aprovados nao precisam ser reenviados
- o analista nao deve precisar revê-los de novo
- a empresa nao deve precisar fazer upload novamente

Regra aprovada:

- nova submissao carrega documentos a partir de `entidade_documento_atual`
- nao depender de reconstruir isso navegando historico de submissao

### 12. Fonte de verdade dos documentos atuais

Opcao 1 aprovada:

- criar uma tabela de projecao chamada `entidade_documento_atual`
- ela guardara o documento oficial atual por entidade + tipo de documento

Decisao:

- usar apenas uma tabela
- nome aprovado: `entidade_documento_atual`

### 13. `INCOMPLETO` nao e mais estado persistido principal

Confirmado:

- `INCOMPLETO` deixa de ser o workflow principal persistido
- o estado persistido do draft sera `EM_EDICAO`
- `INCOMPLETO` pode existir apenas como conceito computado de UI, por exemplo:
  - submissao em edicao com itens obrigatorios faltando

### 14. Submissao enviada fica congelada para a empresa

Confirmado:

- ao entrar em `AGUARDANDO_ANALISE` ou `EM_ANALISE`, a submissao fica imutavel para a empresa
- edicoes, uploads, substituicoes e delecoes so podem ocorrer em `EM_EDICAO`

### 15. Rejeicao de item obrigatorio rejeita a submissao

Confirmado:

- se os dados forem rejeitados, a submissao e rejeitada
- se qualquer documento obrigatorio for rejeitado, a submissao e rejeitada
- o pacote final e aprovado apenas quando todos os itens obrigatorios estiverem aprovados

### 16. Historico detalhado vale a pena

Confirmado:

- vamos usar `submissao_evento`
- historico por submissao e importante para rastreabilidade

### 17. Suporte a documentos multiplos de um mesmo tipo

Confirmado:

- `IDENTIDADE_SOCIO` deve permitir multiplos arquivos
- nao usar o workaround `IDENTIDADE_SOCIO_1`, `IDENTIDADE_SOCIO_2`, etc.

Solucao aprovada:

- adicionar um flag de cardinalidade em `documento_tipo`
- sugestao aprovada: `permite_multiplos boolean`
- `IDENTIDADE_SOCIO` ficara com `permite_multiplos = true`
- os demais tipos seguem `false` por padrao

Conclusao importante:

- nao precisamos de hash para saber o que deletar
- cada arquivo ja tera sua propria identidade por `documento.id`
- cada item da submissao tera sua identidade por `submissao_documento.id`
- hashes continuam uteis para detectar duplicidade, nao para identificar exclusao

## Modelo de dados aprovado

### 1. `submissao`

Representa o pacote/tentativa.

Campos planejados:

- `id`
- `entidade_tipo`
- `entidade_id`
- `status`
- `submissao_origem_id`
- `criado_em`
- `atualizado_em`
- `submetido_em`
- `finalizado_em`
- `criado_por`
- `analista_atual`
- `observacao_analista`

Status aprovados:

- `EM_EDICAO`
- `AGUARDANDO_ANALISE`
- `EM_ANALISE`
- `APROVADA`
- `REJEITADA`

Observacoes:

- uma submissao em `EM_EDICAO` e o draft ativo
- uma entidade pode ter no maximo uma submissao em `EM_EDICAO`
- `submissao_origem_id` referencia a submissao rejeitada que originou a nova tentativa
- `observacao_analista` e opcional, apenas como nota/sumario

### 2. `submissao_dados`

Representa os dados do formulario como item de revisao.

Campos planejados:

- `id`
- `submissao_id`
- `dados_propostos` jsonb
- `hash_dados`
- `carregado_do_live`
- `status_revisao`
- `motivo_rejeicao`
- `revisado_por`
- `revisado_em`
- `criado_em`
- `atualizado_em`

Status aprovados:

- `PENDENTE`
- `APROVADO`
- `REJEITADO`

Regras aprovadas:

- se os dados forem iguais ao estado live atual:
  - `status_revisao = APROVADO`
  - `carregado_do_live = true`
- se a empresa alterar os dados no draft:
  - `status_revisao = PENDENTE`
  - `carregado_do_live = false`
- se `status_revisao = REJEITADO`, `motivo_rejeicao` e obrigatorio

### 3. `submissao_documento`

Representa cada documento incluido na submissao.

Campos planejados:

- `id`
- `submissao_id`
- `documento_id`
- `documento_tipo_nome`
- `obrigatorio_snapshot`
- `validade_informada`
- `status_revisao`
- `motivo_rejeicao`
- `revisado_por`
- `revisado_em`
- `ativo_na_submissao`
- `carregado_de_documento_atual`
- `substitui_submissao_documento_id`
- `criado_em`

Status aprovados:

- `PENDENTE`
- `APROVADO`
- `REJEITADO`

Regras aprovadas:

- documento carregado do estado oficial atual:
  - `status_revisao = APROVADO`
  - `carregado_de_documento_atual = true`
- documento novo enviado no draft:
  - `status_revisao = PENDENTE`
  - `carregado_de_documento_atual = false`
- se `status_revisao = REJEITADO`, `motivo_rejeicao` e obrigatorio

Cardinalidade:

- tipos simples:
  - no maximo um documento ativo por tipo dentro da submissao
- tipos multiplos:
  - varios documentos ativos do mesmo tipo dentro da submissao
  - `IDENTIDADE_SOCIO` e o caso confirmado

### 4. `entidade_documento_atual`

Fonte de verdade dos documentos oficiais atuais.

Campos planejados:

- `id`
- `entidade_tipo`
- `entidade_id`
- `documento_tipo_nome`
- `documento_id`
- `submissao_documento_id`
- `submissao_id`
- `definido_em`

Restricao planejada:

- indice nao-unico em `(entidade_tipo, entidade_id, documento_tipo_nome)`
- tipos multi-instancia como `IDENTIDADE_SOCIO` podem ter varias linhas oficiais atuais
- tipos simples continuam sendo tratados como unicos pela logica da aplicacao

Finalidade:

- determinar qual documento ou conjunto oficial atual vale por entidade/tipo
- alimentar carry-forward em novas submissoes
- servir de base para compliance e regras operacionais

### 5. `submissao_evento`

Historico detalhado por submissao.

Campos planejados:

- `id`
- `submissao_id`
- `tipo_evento`
- `descricao`
- `usuario_email`
- `criado_em`

Exemplos de evento:

- `DRAFT_CRIADO`
- `DADOS_ATUALIZADOS`
- `DOCUMENTO_ADICIONADO`
- `DOCUMENTO_SUBSTITUIDO`
- `DOCUMENTO_REMOVIDO`
- `ENVIADA_PARA_ANALISE`
- `ANALISE_INICIADA`
- `DADOS_APROVADOS`
- `DADOS_REJEITADOS`
- `DOCUMENTO_APROVADO`
- `DOCUMENTO_REJEITADO`
- `SUBMISSAO_APROVADA`
- `SUBMISSAO_REJEITADA`

### 6. `documento`

Permanece como armazenamento do arquivo.

Papel final desejado:

- guardar binario
- nome do arquivo
- content type
- hash
- tamanho
- data de upload
- validade, quando aplicavel

Observacao:

- no modelo final, `documento` nao deve mais ser a fonte primaria do workflow
- o workflow passa a viver em `submissao_dados` e `submissao_documento`

## Regras de negocio aprovadas

### Criacao do draft

Quando a empresa altera uma entidade:

- se existir submissao `EM_EDICAO`, usa essa
- senao cria nova submissao `EM_EDICAO`

Prefill aprovado:

- `submissao_dados`
  - em uma retomada apos rejeicao, prefill com os dados da submissao rejeitada anterior
  - se nao houver submissao rejeitada anterior relevante, prefill com os dados live
- `submissao_documento`
  - carregar a partir de `entidade_documento_atual`

Intencao:

- formulario continua de onde a empresa parou
- documentos aprovados atuais ja aparecem no draft

### Salvar

`Salvar`:

- atualiza apenas o draft
- nao muda entidade live
- nao entra em fila
- nao dispara analise automaticamente

### Upload, substituicao e delecao de documento em `EM_EDICAO`

Regras aprovadas:

- pode adicionar, trocar e deletar documentos livremente enquanto estiver em `EM_EDICAO`
- tipos simples:
  - upload novo substitui o documento ativo daquele tipo na submissao
- tipos multiplos:
  - upload novo adiciona mais uma linha ativa daquele tipo
- delete remove apenas o item selecionado daquela submissao
- deletar documento obrigatorio apenas torna o draft inapto para envio ate ser corrigido

### Enviar para analise

Ao clicar `Enviar para analise`:

- validar a submissao
- se invalida:
  - permanece em `EM_EDICAO`
  - exibir exatamente o que falta
- se valida:
  - muda para `AGUARDANDO_ANALISE`
  - registra `submetido_em`

Validacoes aprovadas:

- dados propostos estruturalmente validos
- documentos obrigatorios presentes
- documentos obrigatorios vigentes quando validade se aplicar
- outras validacoes de negocio que ja existirem

### Inicio da analise

Analista pega a submissao:

- `AGUARDANDO_ANALISE -> EM_ANALISE`
- um analista por vez

### Revisao pelo analista

Analista revisa:

- dados do formulario via `submissao_dados`
- documentos via `submissao_documento`

Regras:

- pode aprovar ou rejeitar dados
- pode aprovar ou rejeitar cada documento
- rejeicao de item exige motivo

### Aprovacao final da submissao

Uma submissao pode ficar `APROVADA` somente se:

- `submissao_dados.status_revisao = APROVADO`
- todos os documentos obrigatorios ativos estiverem `APROVADO`

Ao aprovar:

1. aplicar `dados_propostos` na entidade live
2. atualizar `entidade_documento_atual` com os documentos aprovados daquela submissao
3. marcar submissao como `APROVADA`
4. registrar evento(s)

### Rejeicao final da submissao

Uma submissao fica `REJEITADA` se:

- os dados forem rejeitados
- ou qualquer documento obrigatorio for rejeitado

Ao rejeitar:

- a submissao atual fica imutavel
- nada e promovido para estado oficial
- quando a empresa voltar a editar, criamos uma nova submissao em `EM_EDICAO`

### Promocao de documentos

Decisao aprovada:

- somente a aprovacao final da submissao atualiza `entidade_documento_atual`
- um documento aprovado dentro de uma submissao que terminou rejeitada nao vira automaticamente documento oficial

Motivo:

- mantem consistencia entre pacote aprovado e estado oficial
- evita promocao parcial em pacote rejeitado

## Ponto que surgiu e ainda requer definicao explicita

### Regra de prontidao para `IDENTIDADE_SOCIO`

Decisao fechada:

- `IDENTIDADE_SOCIO` aceita multiplos arquivos
- pelo menos um arquivo e obrigatorio
- arquivos adicionais sao opcionais

Interpretacao operacional:

- se o tipo for obrigatorio e `permite_multiplos = true`, a validacao automatica exige ao menos uma linha ativa daquele tipo na submissao
- a quantidade adicional fica a criterio da empresa e da analise humana

## Impactos tecnicos aprovados

### O fluxo antigo deixara de ser a base principal

O novo modelo substitui para `empresa`, `veiculo` e `motorista`:

- `fluxo_pendencia` como workflow principal
- `INCOMPLETO` como status persistido principal
- documentos ligados a entidade como origem da revisao do analista
- entrada automatica na fila ao salvar/upload/deletar

### Papel residual do legado

Enquanto a implementacao estiver em transicao:

- o legado pode coexistir temporariamente
- mas o alvo final e que o novo fluxo seja a fonte de verdade para essas entidades

## Roadmap completo aprovado

### Fase 1. Schema

- adicionar `submissao`
- adicionar `submissao_dados`
- adicionar `submissao_documento`
- adicionar `entidade_documento_atual`
- adicionar `submissao_evento`
- adicionar suporte a cardinalidade em `documento_tipo`, com flag de multiplos
- criar indices e constraints necessarios

Objetivo:

- colocar toda a estrutura persistente do novo fluxo no banco e no EF

### Fase 2. Servicos de dominio e aplicacao

- criar servicos do novo fluxo de submissao
- criar/recuperar draft em `EM_EDICAO`
- prefill de dados do draft
- carry-forward de documentos via `entidade_documento_atual`
- salvar dados no draft
- upload/substituicao/remocao de documentos no draft
- validacao de prontidao para envio
- envio explicito para analise
- servicos de historico `submissao_evento`

Objetivo:

- ter a logica central funcionando independentemente das paginas

### Fase 3. Paginas da empresa

- substituir leitura/escrita baseada em `fluxo_pendencia`
- usar `submissao`, `submissao_dados` e `submissao_documento`
- adicionar botao `Enviar para analise`
- manter botao `Salvar`
- mostrar status por item
- mostrar motivos de rejeicao por item
- suportar documentos multiplos onde aplicavel

Objetivo:

- a empresa operar 100% pelo novo draft/submissao

### Fase 4. Paginas do analista

- fila baseada em `submissao`
- tela de revisao de uma submissao especifica
- card de revisao para dados
- lista de revisao por documento
- aprovar/rejeitar item por item
- decisao final derivada dos itens
- historico de eventos e contexto da tentativa

Objetivo:

- analista revisar o pacote correto, sem inferencia por pool global de documentos

### Fase 5. Status, conformidade e regras operacionais

- recalcular status com base no novo modelo
- usar `entidade_documento_atual` para documentos oficiais
- manter `INCOMPLETO` apenas como representacao computada de UI
- adaptar dashboard de conformidade
- adaptar regras operacionais que dependem de documentacao valida/aprovada

Objetivo:

- fazer o resto do sistema ler o novo estado oficial corretamente

### Fase 6. Limpeza do legado

- parar de usar `fluxo_pendencia` como fluxo principal para `empresa`, `veiculo`, `motorista`
- remover entrada automatica na fila por upload/delete/save
- remover suposicoes antigas de "ultimo documento por tipo"
- atualizar ou substituir testes antigos
- decidir se campos legados como `documento.fluxo_pendencia_id` e `documento.aprovado_em` seguem temporariamente ou sao aposentados

Objetivo:

- deixar o novo modelo como caminho oficial e reduzir duplicidade de regras

## Checkpoint de implementacao

Esta secao existe para servir como memoria duravel do estado atual da execucao.
Ela deve ser atualizada ao longo do trabalho conforme partes relevantes avancarem ou mudarem.

### Ja implementado

- schema novo criado no codigo e aplicado em `localhost/metroplan`:
  - `eventual.submissao`
  - `eventual.submissao_dados`
  - `eventual.submissao_documento`
  - `eventual.entidade_documento_atual`
  - `eventual.submissao_evento`
- `documento_tipo.permite_multiplos` adicionado e `IDENTIDADE_SOCIO` marcado como multiplo
- modelos EF adicionados no projeto
- `SubmissaoWorkflow` criado com os novos estados
- `SubmissaoService` criado com a primeira camada de servicos do novo fluxo
- `ArquivoService` ja consegue vincular upload ao draft da submissao
- draft consegue ser criado automaticamente quando necessario
- `submissao_dados` ja e preenchido com snapshot live ou snapshot da submissao rejeitada anterior
- `submissao_dados` ja nasce como:
  - `APROVADO` + `carregado_do_live = true` quando os dados equivalem ao live
  - `PENDENTE` quando os dados foram alterados
- carry-forward de documentos funciona assim:
  - primeiro tenta `entidade_documento_atual`
  - se ainda nao houver projecao oficial, usa fallback dos documentos legados ligados diretamente a entidade
- regras de cardinalidade do draft:
  - tipos simples substituem o ativo anterior
  - tipos multiplos mantem varios ativos
- `IDENTIDADE_SOCIO` validado como:
  - ao menos um arquivo obrigatorio quando exigido
  - arquivos extras opcionais
- `EmpresaEntityEditGuardService` ja considera o novo fluxo de `submissao`
  - com fallback para `v_pendencia_atual` durante a transicao
- page models da empresa migrados para o novo draft:
  - `EditarEmpresa`
  - `EditarVeiculo`
  - `EditarMotorista`
- as views de edicao da empresa ja mostram:
  - badge de status da nova submissao
  - botao `Salvar`
  - botao `Enviar para análise`
  - upload disponivel mesmo quando ja existe doc, para permitir substituicao no draft

### Implementado parcialmente

- a UI da empresa ja fala com o novo draft
- a UI do analista ja foi migrada para trabalhar por `submissao_id`
- a fila do analista agora lista `AGUARDANDO_ANALISE` e `EM_ANALISE` a partir de `eventual.submissao`
- a tela de revisao do analista agora carrega:
  - entidade live
  - `submissao_dados`
  - `submissao_documento`
  - `submissao_evento`
- o analista ja consegue:
  - iniciar analise de uma submissao
  - aprovar/rejeitar `submissao_dados`
  - aprovar/rejeitar `submissao_documento`
  - aprovar/rejeitar a submissao final
- a promocao para estado oficial ja foi ligada ao novo caminho:
  - aplica `submissao_dados` na entidade live
  - atualiza `entidade_documento_atual`
- existe compatibilidade temporaria para evitar quebrar o que ainda depende do fluxo antigo
- parte dos construtores e guards ainda tem fallback/overload de compatibilidade
- o bootstrap de documentos no draft ainda usa fallback legado enquanto `entidade_documento_atual` nao foi totalmente populada pelo novo fluxo
- `entidade_documento_atual` deixou de ser unico por tipo:
  - agora aceita varias linhas oficiais atuais para tipos multi-instancia
  - tipos simples continuam sendo mantidos como unicos pela logica de promocao
- regra importante ajustada na aprovacao final:
  - para tipos obrigatorios multi-instancia como `IDENTIDADE_SOCIO`, basta existir ao menos um documento aprovado do tipo
  - documentos extras do mesmo tipo nao passam a bloquear a aprovacao final automaticamente
- ao aprovar uma submissao, a promocao agora sincroniza todo o conjunto oficial atual da entidade:
  - remove o conjunto anterior
  - recria a projecao a partir dos documentos aprovados na submissao
- `EntityStatusService` ja foi reescrito para ler o novo modelo:
  - usa `entidade_documento_atual` como fonte primaria dos documentos oficiais aceitos
  - usa fallback legado de documentos ligados direto na entidade apenas quando ainda nao houver projecao oficial
  - usa a ultima `submissao` apenas para sobrepor o status de UI e o motivo da ultima rejeicao
- regra implementada no status:
  - legalidade operacional vem do estado oficial atual
  - submissao rejeitada mais recente nao invalida sozinha um cadastro oficialmente valido
  - submissao `AGUARDANDO_ANALISE` ou `EM_ANALISE` aparece no status atual, mas nao bloqueia a operacao se o estado oficial continua valido
- `EmpresaConformidadeService` e a central de conformidade agora usam os motivos vindos da nova `submissao`, sem depender de `v_pendencia_atual`
- `_ConformidadeRow` agora distingue:
  - status operacional atual
  - aviso de ultima tentativa rejeitada
- listas `MeusVeiculos` e `MeusMotoristas` passaram a usar o novo `EntityStatusService` para decidir bloqueio de exclusao
- `NovoVeiculo` e `NovoMotorista` deixaram de empurrar o fluxo legado automaticamente:
  - agora criam/atualizam o draft de `submissao`
  - registram os dados propostos no novo modelo
  - redirecionam para a tela normal de edicao do draft
- `Metroplan/Admin/DetalhesAnalista` foi migrado para mostrar e desatribuir `submissao` em vez de `fluxo_pendencia`
- `AnalystReviewService` agora expoe apenas o fluxo novo de `submissao`
- `ArquivoService` agora depende somente de `ISubmissaoService`
- os construtores de compatibilidade das telas `EditarEmpresa`, `EditarVeiculo` e `EditarMotorista` foram removidos
- `EmpresaEntityEditGuardService` deixou de consultar `v_pendencia_atual` como fallback
- `PendenciaService`, `FluxoPendencia`, `VPendenciaAtual` e `WorkflowValidator` foram removidos do app
- `EvaDbContext` nao mapeia mais `fluxo_pendencia` nem `v_pendencia_atual`
- `Documento` nao carrega mais a propriedade `FluxoPendenciaId`

### Ainda nao implementado

- nova bateria principal de testes do fluxo de submissao completo

### Compatibilidades temporarias intencionais

- `EmpresaEntityEditGuardService` ainda consulta `v_pendencia_atual` como fallback se nao houver `submissao`
- alguns construtores mantem assinaturas antigas apenas para nao quebrar chamadas existentes durante a migracao
- `ArquivoService` aceita operar com e sem `ISubmissaoService`, para convivencia transitória entre o modelo antigo e o novo
- o draft pode ser preenchido com documentos legados da entidade quando ainda nao houver `entidade_documento_atual`

### Proximos passos seguros

1. Reescrever conformidade/status sobre o novo fluxo
2. Passar dashboards/consultas operacionais restantes a ler `entidade_documento_atual` e `submissao`
3. Reduzir fallbacks e compatibilidades temporarias do legado
4. Aposentar de vez `fluxo_pendencia` para `empresa`, `veiculo` e `motorista`
5. Escrever a nova bateria principal de testes do fluxo de submissao

### Compatibilidades legadas que ainda restam de proposito

- as tabelas/view legadas ainda podem continuar existindo no banco local por enquanto, mas nao fazem mais parte do runtime do app
- `WorkflowStatus` continua existindo como conjunto de labels/estados de UI para a camada de apresentacao

### Estado atual da verificacao

- `dotnet build Eva.csproj` esta verde
- `dotnet build` na raiz esta verde novamente
- a nova bateria focada no fluxo de `submissao` ja esta compilando e executando

### Regra operacional para memoria duravel

- este arquivo deve continuar sendo atualizado durante o trabalho
- toda decisao importante, mudanca arquitetural relevante ou compatibilidade temporaria significativa deve ser registrada aqui
- se o contexto da conversa ficar longo, este arquivo deve ser relido antes de continuar

## Combinado de trabalho durante a implementacao

Instrucao dada pelo usuario:

- fazer mudancas reais em banco e codigo
- trabalhar diretamente em `localhost/metroplan`
- quando houver duvida relevante de produto/regra, parar e perguntar
- nao supor comportamento nao acordado

### Politica de testes durante a transicao

Decisao adicional aprovada:

- nao gastar capacidade tentando preservar testes antigos que validam o fluxo legado
- durante a transicao, manter apenas verificacoes leves para nao introduzir quebra obvia
- continuar usando:
  - `dotnet build`
  - testes novos e focados no modelo de `submissao`
- nao perseguir testes antigos que falham apenas porque descrevem o comportamento antigo
- quando o novo fluxo estiver funcionalmente completo, escrever a nova bateria principal de testes voltada ao modelo de submissao

Interpretacao pratica:

- testes legados so devem influenciar a implementacao se revelarem um bug real no novo desenho
- divergencias esperadas entre o fluxo antigo e o novo nao devem consumir tempo de adaptacao agora

## Resumo executivo final

O sistema sera redesenhado para usar uma submissao versionada e explicita por tentativa, com:

- uma submissao em edicao por entidade
- envio explicito para analise
- dados do formulario como item de revisao proprio
- documentos como itens de revisao individuais
- motivo obrigatorio por item rejeitado
- historico por submissao
- documentos oficiais atuais mantidos em `entidade_documento_atual`
- documentos ja aprovados carregados para novas tentativas
- suporte real a tipos multiarquivo, como `IDENTIDADE_SOCIO`

Esse documento deve ser tratado como fonte de verdade do desenho acordado durante a implementacao.

## Checkpoint de testes e estabilizacao

### O que foi feito

- os testes legados que validavam `fluxo_pendencia` foram aposentados do projeto de testes
- o projeto `Eva.Tests` voltou a compilar sobre o runtime novo
- foram mantidos e/ou escritos testes focados no fluxo novo de `submissao`, cobrindo:
  - `SubmissaoService`
  - `AnalystReviewService`
  - `ArquivoService`
  - `EntityStatusService`
  - page models principais de edicao da empresa

### Bug real encontrado pelos testes novos

- o primeiro upload de documento para uma entidade sem draft ainda criado estava duplicando o mesmo arquivo dentro de `submissao_documento`
- causa:
  - o upload criava o `documento` e o vinculava na entidade
  - ao criar o draft, o bootstrap legado carregava esse mesmo arquivo
  - em seguida `VincularDocumentoAoDraftAsync` adicionava outra linha para o mesmo `documento_id`
- correcao aplicada:
  - `SubmissaoService.VincularDocumentoAoDraftAsync` agora detecta quando o mesmo `documento_id` ja entrou no draft durante o bootstrap
  - nesses casos, a linha existente e reaproveitada e convertida para `PENDENTE`, sem duplicacao

### Verificacao atual

- `dotnet build` na raiz: verde
- `dotnet test Eva.Tests\\Eva.Tests.csproj --filter "AnalystReviewServiceIntegrationTests|ArquivoServiceIntegrationTests|EntityStatusServiceIntegrationTests|EmpresaEditPageModelsIntegrationTests|SubmissaoServiceIntegrationTests"`: verde

### Politica que continua valendo

- continuar priorizando testes novos e focados no fluxo de `submissao`
- nao gastar tempo tentando ressuscitar testes que descrevem o comportamento legado substituido

### Automacao de smoke local

- foi criado um smoke test manual automatizado em `scripts/smoke-auth.ps1`
- ele:
  - semeia uma empresa e usuario descartaveis no banco local
  - executa o fluxo de `ConfirmarAcesso`
  - testa login invalido e login valido
  - valida as paginas principais da area da empresa
- objetivo:
  - reduzir o custo de verificar os primeiros passos do fluxo local sem depender do navegador

## Checkpoint de UX de rejeicao

### Problemas encontrados manualmente

- apos rejeicao de uma submissao de `motorista`, a lista `MeusMotoristas` nao estava refletindo `Rejeitado`
- na tela de edicao, apenas a rejeicao dos dados aparecia no topo
- os documentos rejeitados nao exibiam seu status individual nem o motivo de rejeicao

### Correcao aplicada

- `EntityStatusService` passou a priorizar a ultima `submissao` rejeitada para o status apresentado em listas e conformidade, sem perder a distincao de legalidade operacional
- foi criado `DocumentoEdicaoItemVm` para a camada de edicao expor:
  - documento
  - status de revisao
  - motivo de rejeicao
  - indicador de item carregado do estado oficial
- `SubmissaoService.GetDocumentosParaEdicaoAsync` agora monta os documentos da tela de edicao a partir do draft atual ou, na ausencia dele, da ultima submissao
- as telas de edicao de `empresa`, `veiculo` e `motorista` passaram a renderizar badge de status por documento e motivo individual para documentos rejeitados

### Verificacao

- `dotnet msbuild Eva.csproj /t:Compile /p:UseAppHost=false`: verde
- `dotnet test Eva.Tests\\Eva.Tests.csproj --no-build --filter "EntityStatusServiceIntegrationTests|SubmissaoServiceIntegrationTests"`: verde
- observacao:
  - o build normal do projeto de testes pode falhar se `Eva.exe` ou `Eva.dll` estiverem travados pelo app em execucao ou pelo Visual Studio

### Ajuste adicional de apresentacao

- `MeusMotoristas` e `MeusVeiculos` ainda priorizavam `health.IsLegal` na renderizacao do badge
- isso mascarava `CurrentStatus = Rejeitado` e exibia `Legalizado` para entidades com ultima tentativa rejeitada
- a precedencia visual foi corrigida:
  - `Rejeitado`
  - `AguardandoAnalise`
  - `EmAnalise`
  - `Legalizado`
  - `Incompleto`

## Checkpoint de UX da tela de revisao do analista

- o botao `Aprovar submissao` deve permanecer desabilitado enquanto existir qualquer item rejeitado
  - dados rejeitados
  - ou qualquer documento rejeitado
- os motivos de rejeicao nao devem depender de validacao implícita do page model
- cada formulario de rejeicao valida apenas o seu proprio campo:
  - `MotivoRejeicaoDados`
  - `MotivoRejeicaoDocumento`
  - `ObservacaoRejeicaoSubmissao`
- os campos de rejeicao tambem foram marcados como `required` no HTML para feedback imediato na interface

## Checkpoint de remocao dos fallbacks legados de documento

- `empresa`, `veiculo` e `motorista` nao usam mais documentos legados vinculados diretamente a entidade como fonte de verdade no runtime
- o fluxo agora depende apenas de:
  - `submissao`
  - `submissao_documento`
  - `entidade_documento_atual`
- remocoes aplicadas:
  - `SubmissaoService` nao preenche mais drafts a partir de tabelas legadas
  - `SubmissaoService.GetDocumentosParaEdicaoAsync` nao faz mais fallback para documentos legados
  - `ArquivoService` nao grava mais `documento_empresa`, `documento_veiculo` e `documento_motorista`
  - `EntityStatusService` nao consulta mais documentos legados para calcular status/conformidade
- `viagem` continua fora do fluxo de submissao e ainda usa o caminho proprio de documentos
- o que ainda resta:
  - os `DbSet` e modelos de link legado continuam no projeto apenas como infraestrutura remanescente, principalmente por causa de `viagem` e por ainda nao termos feito uma limpeza estrutural final do banco/modelos

### Verificacao

- `dotnet msbuild Eva.csproj /t:Compile /p:UseAppHost=false /p:BaseOutputPath=C:\\Sources\\Eva\\artifacts\\build-verify\\`: verde
- `dotnet msbuild Eva.Tests\\Eva.Tests.csproj /t:Compile /p:UseAppHost=false /p:BaseOutputPath=C:\\Sources\\Eva\\artifacts\\testbuild-verify\\`: verde
- `dotnet test Eva.Tests\\Eva.Tests.csproj --no-build --filter "ArquivoServiceIntegrationTests|SubmissaoServiceIntegrationTests|EntityStatusServiceIntegrationTests"`: verde

## Checkpoint de live-flow testing exaustivo

- foi criado o harness `scripts/live-flow-exhaustive.ps1`
- ele executa contra o app local em execucao usando contas reais locais:
  - empresa: `net@net`
  - analista: `ana@ana`
  - admin: `adm@adm`
- ele valida por HTTP real com anti-forgery token e faz asserts complementares no banco local

### Cobertura atual do harness

- disponibilidade basica das rotas autenticadas de empresa, analista e admin
- fluxo de `empresa`:
  - salvar draft
  - upload de `CARTAO_CNPJ`
  - enviar para analise
  - analista inicia analise
  - analista rejeita dados
  - analista rejeita documento
  - analista rejeita submissao
  - empresa volta a enxergar os dois motivos de rejeicao
- fluxo completo de `motorista`:
  - criar
  - upload inicial de `CNH`
  - enviar
  - analista rejeita dados e documento
  - analista rejeita submissao
  - lista da empresa mostra `Rejeitado`
  - tela de edicao mostra motivos
  - empresa exclui documento rejeitado
  - empresa faz novo upload
  - empresa salva ajustes
  - empresa reenvia
  - analista aprova dados
  - analista aprova documento
  - analista aprova submissao
  - banco passa a ter `entidade_documento_atual`
  - lista da empresa mostra `Legalizado`
- fluxo de `veiculo`:
  - criar
  - upload inicial de `CRLV`
  - enviar
  - analista aprova dados
  - analista aprova documento
  - analista aprova submissao
  - banco passa a ter `entidade_documento_atual`
  - lista da empresa mostra `Legalizado`

### Comportamento importante do harness

- antes do fluxo de `empresa`, ele limpa submissões nao aprovadas da empresa de teste para evitar que uma execucao interrompida deixe a tela travada em `EM_ANALISE`
- para `motorista` e `veiculo`, ele cria entidades novas com identificadores unicos por execucao
- o foco do script e capturar regressões de fluxo real, nao apenas disponibilidade de rota

### Ultima execucao validada

- `Basic route availability`: OK
- `Company reject flow`: OK
- `Motorista reject, fix, approve`: OK
- `Veiculo create and approve`: OK
