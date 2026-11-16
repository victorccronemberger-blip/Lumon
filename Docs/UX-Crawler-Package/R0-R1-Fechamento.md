# Lumon · fechamento de R0 e R1

Candidata atual em **Lumon-Recomeco**, Unity 6000.3.27f1. Este registro usa as entregas UX-001 a UX-004 do roadmap original. A direção vigente para a Guardiã é arte 2D pintada. As provas antigas são históricas e cada gate exige verificação na base nova.

## UX-001 · Inventário e fundação

- [x] Criar e compilar o projeto Unity independente.
- [x] Confirmar leitura pelo MCP do Claude Code.
- [x] Registrar nesta candidata os gates e separar a evidência obtida na build anterior.
- [ ] Recriar o Bootstrap e verificar câmera, estações, limites e entrada no Hub.
- [ ] Registrar a versão de cada candidata, localização dos saves de QA e evidências de execução.

## UX-002 · Referência e integração visual

- [x] Recompor e verificar o ambiente em camadas no HubWorld. A entrega inclui a árvore da praça, a árvore de moldura, três bandeiras, três lanternas e a fogueira. [Guia e evidências do HubWorld](../HubWorld.md).

- [x] Integrar a primeira pose 2D e medir o enquadramento em 720p e 1080p na câmera fixa do Unity. [Validação da arte 2D](../Guardiao2D.md).
- [x] Integrar e verificar as oito poses de repouso, incluindo diagonais, em 720p. Confirmar velocidade, direção ao parar, câmera fixa e mesma linha dos pés. [Capturas de R0](../../Art/Guardian2D/review/idle-directions/unity-oito-rumos.png).
- [ ] Integrar e validar os ciclos de caminhada em cada um dos oito rumos. [Primeira ficha de entrega, caminhada sudoeste](../Entregas/UX-002-Guardia-Sudoeste.md).
- [ ] Revisar a Guardiã 2D no ateliê, no Hub e na expedição. Verificar rosto, cabelo, encaixe de roupa e acessórios, os oito rumos e contato dos pés.
- [ ] Verificar a composição do Observatório, os Lumons e a arte usada em jogo contra as referências aprovadas para cada asset.
- [x] Confirmar spawn, limites, oclusão e distância de interação na praça com câmera fixa. [Teste de circulação do Hub](../../Art/HubWorld/review/traversal.json).

## UX-003 · Interface no Hub e ateliê

- [x] Aceite visual da praça, equipe, configurações e atlas do HubWorld em 16/11/2026. [Ficha da build aprovada](../Entregas/HubWorld-Aceite-Visual.md).
- [x] Integrar o acesso por E e as funções da barraca no executável. Verificar escalação, fusão, aprimoramento, compra, incubação, eclosão e persistência. [Entrega da barraca](../Entregas/UX-003-Barraca-da-Equipe.md).
- [x] Verificar encaixe das quatro abas em PT-BR, EN e ES a 1280×720, 1440×900 e 1920×1080. Confirmar eclosão animada, foco e a apresentação com movimento reduzido. [Evidência de interface](../../Art/HubWorld/review/design/player-design-1080.txt).

- [ ] Entregar HUD, atlas da expedição, equipe, configurações e ateliê. Percorrer ida, retorno, cancelar, aplicar e reabrir.
- [ ] Verificar mouse e teclado, foco visível e restaurado, prompts e conflito entre interação do mundo e menus.
- [ ] Rever leitura do livro e demais interfaces em 1280×720, 1920×1080 e 1920×1200, a 100%, 125% e 150%.
- [ ] Verificar PT-BR, EN e ES, incluindo textos dinâmicos, bloqueios, tooltips, confirmação e persistência do idioma. Textos devem estar disponíveis no catálogo de localização.
- [ ] Salvar todas as opções de personalização e conferir a aparência em ateliê, Hub e primeira expedição.

## UX-004 · Primeira sessão e entrega Windows

- [ ] Percorrer menu → criar perfil → Hub → equipe → Vale → explorar/coletar → farol → invocar/evoluir → resultado → retorno → recarregar.
- [ ] Verificar vitória, derrota, abandono, pagamento único, perfil somente leitura, persistência e telemetria local.
- [ ] Rodar a primeira sessão com áudio habilitado e inspecionar o problema de áudio do registro anterior.
- [ ] Gerar Windows x64 e reproduzir a primeira sessão no executável com controles reais e perfil isolado. Empacotar build e evidências.
- [ ] Aplicar o piloto com cinco pessoas. O aceite exige que quatro iniciem uma expedição em até três minutos e expliquem explorar/comandar sem ajuda. Pesquisa pendente por decisão do usuário.

O [protocolo de entrega](../Protocolo-de-Entrega.md) define responsáveis, evidências e critérios de aceite. R2 e monetização seguem no roadmap fora da entrega de R0/R1.
