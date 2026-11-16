# Barraca da Equipe

Caminhe até a barraca da praça e pressione **E**. O prompt aparece ao entrar no alcance. O atalho “Reunir equipe” também abre a barraca. Na mesa de expedições e na porta do Observatório, a tecla continua sendo **F**.

## Funções da barraca

| Aba | Regra inicial |
| --- | --- |
| Equipe | Selecione de um a três Lumons. Salvar aplica a composição ao Hub. Ovos chocados ampliam a coleção e podem ser selecionados. |
| Fusão | Escolha um principal e um material do mesmo tipo. O material precisa estar fora da equipe salva. A confirmação consome o material e 35 moedas, aumentando o grau do principal até 3. |
| Aprimorar | Escolha um Lumon. O aprimoramento custa 30 moedas, eleva o nível em um e tem limite no nível 5. |
| Ovos | Escolha o tipo do ovo por 25 moedas. Coloque um ovo por vez na incubadora. Após 18 segundos, a ação Chocar incorpora o Lumon à coleção. |

O saldo inicial é de **150 moedas do jogo**. O relógio da incubação usa o horário de término. A equipe, os ovos, o saldo, os níveis e os graus são gravados no perfil local. Perfis anteriores migram a composição da equipe salva. Fable 5.2 implementou o estado da barraca. Codex integrou a interface, a interação e a verificação no Unity.

## Direção visual

A barraca se abre em 300 ms sobre a moldura pintada de ouro e jade. O Higgsfield produziu a moldura, os três cartões elementais e as teclas a partir da referência enviada. O Unity sobrepõe os retratos oficiais, o texto localizado e os estados de seleção, foco e pressão. Cada ovo tem cor e selo próprios, visíveis no balcão e na incubadora. A fusão mostra os dois participantes antes da confirmação. O preço, o saldo, o tempo restante e o resultado ficam junto à escolha.

As pranchas em `Art/HubWorld/source/` registram as gerações Higgsfield `ec5c1829-f69f-4c0f-bd7e-253b5bd2a825` (moldura) e `dee50037-6fb0-427a-a002-c4e0737d5fb6` (cartões e teclas). O script `Art/HubWorld/prepare_workshop_higgsfield.py` prepara os recortes usados em jogo.

A eclosão separa as duas partes da casca e revela o retrato do novo Lumon. O nome aparece depois que o retrato entra em cena. “Ver equipe” e “Voltar” nas abas devolvem o foco à escalação. Ao fechar a barraca com Esc, o foco volta ao atalho na praça e a movimentação é liberada. A opção de movimento reduzido exibe a imagem final imediatamente.

## Validação

- [Escalação e botões no executável](../../Art/HubWorld/review/design/design-pt-team.png) e [painel de eclosão](../../Art/HubWorld/review/workshop/workshop-hatch.png)
- [Eclosão capturada no executável](../../Art/HubWorld/review/workshop/workshop-reveal.gif) e [versão com movimento reduzido](../../Art/HubWorld/review/workshop/workshop-hatch-reduced.png)
- [Teclado e mouse no executável](../../Art/HubWorld/review/player-input.txt). O teste abre a barraca com E, compra, incuba e choca um ovo com cliques, confirma a eclosão com Enter, usa Tab e Enter para abrir Fusão e comprova a devolução do foco ao voltar e ao fechar com Esc.
- [Foco ao retornar à aba Equipe](../../Art/HubWorld/review/workshop-team-focus-input.png)
- [Compra e incubação](../../Art/HubWorld/review/workshop/workshop-eggs.png)
- [Fusão com confirmação](../../Art/HubWorld/review/workshop/workshop-fusion.png)
- [Aprimoramento](../../Art/HubWorld/review/workshop/workshop-improve.png)
- [Transações no executável](../../Art/HubWorld/review/workshop/workshop-play.txt) e [reabertura](../../Art/HubWorld/review/workshop/workshop-reload.txt)

Para repetir, rode `Art/HubWorld/review_hub_workshop.ps1` no projeto compilado. O perfil de teste é isolado. O roteiro compra dois ovos, verifica a incubadora ocupada, espera o prazo real, choca um ovo, edita a equipe, testa a proteção do material, confirma a fusão, aprimora o Lumon e reabre o jogo com outra incubação em andamento.
