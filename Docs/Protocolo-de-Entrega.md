# Protocolo de entrega do Lumon

A meta cobre R0 e R1 no projeto **Lumon-Recomeco**. O [checklist de fechamento](UX-Crawler-Package/R0-R1-Fechamento.md) define o escopo e aponta para as evidências. A revisão parte da candidata que o usuário pode abrir no Unity ou executar no Windows.

## Responsabilidades

| Papel | Responsável | Entrega |
| --- | --- | --- |
| Direção executiva, UX e design | Codex | Define a ordem do roadmap, a experiência do jogador, a direção visual e o aceite. Faz a integração e revisa a candidata na câmera e nos fluxos reais. |
| Produção de imagem | Higgsfield | Produz artes de origem e material visual a partir do brief. Guarda o ID do job, o modelo usado e o arquivo de origem. |
| Engenharia de software | Claude Code · Fable 5.2 | Implementa sistemas, integrações, persistência, lógica de jogo e testes. Entrega revisão reproduzível com indicação das áreas afetadas. |
| Animação e arte em movimento | Claude Code · Opus 5.5 | Define e produz os ciclos a partir dos assets aprovados, prepara os quadros, marca apoios e transições e entrega os arquivos para integração no Unity. Pode usar o Higgsfield como fonte visual. |

Os IDs oficiais são `claude-fable-5-2` e `claude-opus-5-5`. Registrar o identificador que o ambiente confirma em cada execução. Se a rota ou o modelo solicitado ficar indisponível, registrar o impacto e seguir com as partes independentes. [Catálogo oficial da Anthropic](https://platform.claude.com/docs/en/models/overview).

## Verificação das ferramentas

Em 15/11/2026, Claude Code 2.1.325 respondeu a uma chamada curta com cada ID solicitado. O campo `modelUsage` confirmou `claude-fable-5-2` e `claude-opus-5-5`, ambos com saída bem-sucedida. A conexão com o Higgsfield também respondeu ao teste de leitura.

## Fluxo de cada entrega

1. **Definir o aceite.** Vincular a tarefa ao roadmap, nomear o responsável e escrever o comportamento ou resultado que o jogador deve observar. Identificar a referência visual e os caminhos atingidos.
2. **Conferir a fonte.** Identificar o arquivo e a revisão que entram na entrega. Para arte, registrar a origem e conferir identidade visual, dimensões, transparência e recorte. Para software, identificar a revisão, a entrada normal do jogador e os estados que precisam continuar funcionando.
3. **Reproduzir na candidata.** Rodar o caminho afetado e guardar o comando ou roteiro, as condições do teste e o resultado. Registrar versão do Unity, build ou revisão e, quando aplicável, cena, perfil, idioma, resolução e escala da UI.
4. **Inspecionar e retestar.** Comparar na mesma câmera e resolução. Registrar defeitos com passos de reprodução, impacto, correção e resultado do reteste. Uma captura de conceito prova a direção visual. A captura da candidata mostra a integração. A execução da jornada prova o comportamento.
5. **Fechar pelo resultado.** Atualizar a linha do roadmap com a evidência do aceite. Uma entrega ainda em revisão continua marcada como tal. No fechamento de R0/R1, anexar a build x64 e o roteiro que confirma a primeira sessão.

## Critérios aplicáveis

| Entrega | Evidência de aceite |
| --- | --- |
| Arte e animação | Fonte e sprites efetivamente carregados. Capturas do Unity em 720p e 1080p, com enquadramento e recortes em escala real. Para movimento, verificar quadros, ida e volta do loop, apoio, deslizamento do pé, retorno ao repouso, transições, oito rumos e detalhes assimétricos. |
| HUD, atlas, equipe, configurações e ateliê | Percurso completo por mouse e teclado, foco, confirmação, volta e bloqueio quando aplicável. Conferir legibilidade e corte em PT-BR, EN e ES nas resoluções e escalas do checklist. |
| Sistemas e primeira sessão | Exercitar entrada real, vitória, derrota, abandono, gravação, recarga e falhas aplicáveis com perfis isolados. Guardar logs e provar que recompensas só são aplicadas uma vez. |
| Build Windows x64 | Identificar o executável testado. Reproduzir a primeira sessão, o retorno ao Hub, o áudio e a recarga no binário distribuível. |
| Pesquisa | O piloto de cinco pessoas tem roteiro e critérios próprios. O aceite técnico pode avançar enquanto o piloto segue pendente. A meta de 100% inclui o resultado da pesquisa. |

## Registro reutilizável

Para cada entrega, usar uma ficha com estes campos.

- **Tarefa e responsável** — item do roadmap, resultado esperado, escopo e modelo efetivamente usado.
- **Fontes e revisão** — referência de arte ou comportamento, caminhos e job, commit ou hash pertinente.
- **Reprodução** — candidata, ambiente, pré-condições e comandos ou passos.
- **Evidência** — caminhos para capturas, logs e saídas com a condição verificada em cada uma.
- **Decisão** — estado atual, falhas abertas, resultado de reteste e próximo passo.

A [caminhada sudoeste da Guardiã](Entregas/UX-002-Guardia-Sudoeste.md) é a primeira ficha preenchida.
