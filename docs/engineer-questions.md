# Questions for the agronomy engineers

Open questions to resolve with the engineers before or during the next meetings. Each one names the evidence that
raised it and the current behaviour, so the answer can be applied directly. Mark an item `[x]` and add the answer
underneath when it is resolved. Answers already given by the engineers' domain reference document are marked with it. Public repository: no client, contractor or person names here.

## Work orders (OT)

- [x] Q1. Can a client issue OTs under more than one commercial firm? Evidence: 29 of 31 firms in the workbook carry
  the client's own name; 2 do not match any client. Current behaviour: each client gets a firm with its name, and
  more firms can be added.
  Answer (domain reference, point 1): yes, a client may be made up of several commercial firms. Still open: the 2
  workbook firms that match no client — which client do they belong to?
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
  Partly answered (domain reference, point 2): a plot's area can change between campaigns. Still open: which area an
  OT should propose when the campaign area and the master area differ.
- [x] Q13. Season columns hold crop sequences such as "TRIGO/SOJA". Confirm it means first and second crop of the
  same campaign, and how pastures lasting several campaigns should be recorded.
  Answer (domain reference, point 3): one or two crops per campaign, each for income (renta) or service (servicio);
  pastures can last more than one campaign and are recorded in each campaign they occupy.
- [ ] Q14. Client notes list product stock held by each client. Should the system track client stock and discount it
  when an OT is issued?
- [ ] Q15. Field tenure (own, fixed rent, percentage rent): who owns this data and does it affect any document?
  Partly answered (domain reference, point 2): the three tenure types are confirmed. Still open: does tenure affect
  any document or report?

## Brand

- [ ] Q16. Does the engineering company have a brand manual, logo files and a colour palette? They will style the
  site and every document it produces (OT PDF first).

## Analyses and providers

- [ ] Q17. Which soil parameters, units and sampling depths do you use (e.g. pH, organic matter %, P ppm, N-NO3 ppm,
  S-SO4 ppm, Zn, CEC; 0-20 / 20-40 / 40-60 cm)? Current behaviour: free parameter name with a suggested list, value,
  unit, optional depth and station.
- [ ] Q18. Seeding rate: confirm the formula seeds/ha = target plants/ha / (germination % x expected field emergence
  %), kg/ha = seeds/ha x thousand-seed weight (g) / 1,000,000, which germination value to use (with or without seed
  treatment) and the usual emergence efficiency per crop. Current behaviour: the calculator uses germination with
  treatment when present, otherwise without, and asks for the efficiency (default 90%).
- [ ] Q19. Supplier quotes: are prices compared with or without VAT, and in ARS or USD? Do you need the exchange
  rate of the day to compare mixed currencies? Current behaviour: each quote has one currency; totals are compared
  only within the same currency.
- [ ] Q20. "Precios de referencia de productos que tenemos": are these your own reference prices per input, each
  supplier's list prices, or both? Current behaviour: each input keeps its reference cost and each supplier can
  have dated prices per input.
