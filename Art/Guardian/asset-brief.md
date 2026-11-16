# CH_Guardian — Guardiã do Observatório

| Campo | Referência |
| --- | --- |
| Asset | CH_Guardian |
| Categoria | Personagem bípede adulto estilizado |
| Vistas | Frente 0°, perfil esquerdo −90° e costas 180°, elevação 0°, ortográficas aproximadas. A figura ocupa cerca de 95% da imagem. Linha de apoio na coordenada vertical 666 de 688. |
| Escala | 1,80 m do topo do cabelo ao chão. Um pixel da folha equivale a 2,7565 mm. Blender em metros, Z para cima, −Y para a frente e origem no chão entre as botas. |
| Silhueta | Coque baixo, gola alta de linho, capa ameixa assimétrica, bolso de mapas no cinto, botas com cano alto. |
| Materiais | Pele e tecido foscos. Metal pintado no broche e na fivela. O cristal turquesa concentra o contraste perto do rosto. |
| Articulação | Esqueleto humanoide único. Repouso e caminhada em oito rumos com o mesmo modelo. Cabelo, gola e casaco em volumes independentes no master. |
| Inferências | O perfil só mostra o lado esquerdo. A envoltura posterior sob a capa, a posição em profundidade das mãos e o desenho do solado precisam de interpretação. |
| Destino | Unity 6000.3.27f1, FBX. Câmera fixa ortográfica com inclinação de 30°, área vertical de 8,20 m, 137 px projetados em 720p para uma régua de 1,80 m. Orçamento inicial de até 30 mil triângulos no LOD0. |

## Proporções da folha

As alturas são relativas a 653 px entre a linha de chão e a de topo usada na calibração. Os pontos de articulação ocultos são estimativas.

| Ponto ou seção | Coordenada na folha | Proporção | Medida a 1,80 m |
| --- | --- | --- | --- |
| Queixo na frente | Linha 114 | 84,5% da altura | 1,52 m |
| Linha dos ombros | Linha 148 | 79,3% da altura | 1,43 m |
| Largura com a capa | Frente, linha 148, 148 px | 22,7% da altura | 0,41 m |
| Profundidade com a capa | Perfil, linha 148, 86 px | 13,2% da altura | 0,24 m |
| Cintura na borda do casaco | Frente, linha 260, 97 px | 14,9% da altura | 0,27 m |
| Linha do cinto | Linha 273 | 60,2% da altura | 1,08 m |
| Largura do quadril encoberto | Frente, linha 321, 147 px | 22,5% da altura | 0,41 m |
| Gancho da calça (inferido) | Linha 351 | 48,2% da altura | 0,87 m |
| Joelho (inferido) | Linha 470 | 30,0% da altura | 0,54 m |
| Cano da bota | Linha 543 | 18,8% da altura | 0,34 m |
| Ponta do pé ao calcanhar | Perfil, linha 657, 107 px | 16,4% da altura | 0,29 m |

## Construção

- Base interna com tronco, pescoço, cabeça, braços, mãos, pernas e pés. A maior parte é deformada pelo esqueleto humanoide.
- Roupa em camadas. Gola de linho e mangas sob o casaco, capa por cima dos ombros, calça sob o cinto, pernas entrando nas botas. Capa presa na gola e acompanhando o tronco e clavículas.
- Coque e mechas presos à cabeça. Broche no peito e bolso de mapas no quadril com pontos de montagem próprios para equipar ou remover.
