# Questions for the agronomy engineers

Open questions to resolve with the engineers before or during the next meetings. Each one names the evidence that
raised it and the current behaviour, so the answer can be applied directly. Mark an item `[x]` and add the answer
underneath when it is resolved. Public repository: no client, contractor or person names here.

## Work orders (OT)

- [ ] Q1. Can a client issue OTs under more than one commercial firm? Evidence: 29 of 31 firms in the workbook carry
  the client's own name; 2 do not match any client. Current behaviour: each client gets a firm with its name, and
  more firms can be added.
- [ ] Q2. In about 3% of historical OTs the products or doses differ between plots of the same OT. Is that one OT,
  or should it be split into one OT per product mix? Current behaviour: inputs and doses apply to every plot of the OT.
- [ ] Q3. 32 historical OTs show two labors, 116 two dates and 19 two crop stages. Data-entry mistakes, or a real
  case to support? Current behaviour: one labor, one date and one stage per OT.
- [ ] Q4. Can an OT ever need more than one contractor? Evidence: every historical OT has exactly one.
- [ ] Q5. 212 historical spraying OTs have no products. Valid (e.g. the products are on the observation), or
  incomplete? Current behaviour: inputs are optional for every labor.
- [ ] Q6. Confirm that numbering is per commercial firm and that it should continue from the workbook's last number.
- [ ] Q7. The issued OT PDF shows the signature of the responsible engineer. Which engineers sign, and may we store
  each one's scanned signature to print it?
- [ ] Q8. Some OTs include a map image with the area to treat marked in red. Where does it come from (Google Earth,
  a GIS, a screenshot), and should each OT allow attaching an image?
- [ ] Q9. The footer lists contact phones for questions. Fixed for the company, or the engineers on each OT?
- [ ] Q10. The OT shows a warning derived from the crop's genetic event (e.g. tolerant to a herbicide). Should we keep
  a genetic event table and warn automatically when an input conflicts with it?
- [ ] Q11. How does an OT relate to a digital agronomic prescription (receta agronómica digital)? Does every OT need
  one, and who issues it?

## Clients, fields and plots

- [ ] Q12. The plot master stores one area, and the OT stores the plot area and the worked area. When they differ,
  which area is authoritative, and does it change per campaign?
- [ ] Q13. Season columns hold crop sequences such as "TRIGO/SOJA". Confirm it means first and second crop of the
  same campaign, and how pastures lasting several campaigns should be recorded.
- [ ] Q14. Client notes list product stock held by each client. Should the system track client stock and discount it
  when an OT is issued?
- [ ] Q15. Field tenure (own, fixed rent, percentage rent): who owns this data and does it affect any document?

## Brand

- [ ] Q16. Does the engineering company have a brand manual, logo files and a colour palette? They will style the
  site and every document it produces (OT PDF first).
