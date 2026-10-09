# Current local interaction design

Eligible raw feature IDs use immediate checkboxes: check to request enabled,
uncheck to request disabled. Windows default is a distinct action that removes
the explicit override; it must not be implied merely by an unchecked control.
There is no queue, Review page, staged footer or extra app confirmation. Windows
UAC remains the authority for elevation. Pending, successful, failed and uncertain
outcomes must remain visible without pretending a request already succeeded.

Catalog browsing remains nonmutating. Historical feature archives are read-only,
and native Windows settings and guides open only documented destinations or source
pages. Their cards are not presented as additional writable feature toggles.
History is a read-only record of operation results, with no replay or scoped undo.
Restore default is a secondary action for the selected feature, removing only its
explicit user override.

One searchable list contains the sourced editorial entries and pinned raw IDs;
there is no separate All IDs view. Three visible checkbox filters replace filter
dropdowns: Known content (153) and Historical references (60) start checked, while
Show unknown (16,944) starts unchecked. Filter changes affect visibility only. They are
separate from the selected feature’s Enable checkbox, which requests a write.

An exact numeric match hidden by the active filters produces a prompt to check
the appropriate filter. It never silently appears or changes the filter selection.
A separate Enter ID field accepts one decimal, nonzero uint32 value. Pasting and
pressing Enter explicitly requests read-only inspection of that ID, even when it
is outside the pinned dictionary. This does not bypass or change search filters,
run shell input, or execute a feature change. Accepted syntax is not evidence of
feature validity or availability.

Pagination always limits the list to twelve cards, including unknown entries.
Search and visibility filters sit above the cards; pagination sits below them.

Known purpose is not proof of Windows support. Unknown purpose is missing
editorial evidence, not a statement about whether observed system state is known.

Localized search remains available. Cards show short previews; full instructions, evidence, risks, restart and
restoration limits live in the detail pane. At compact widths, catalog and details
are separate views. Sixteen languages, Arabic RTL, LTR numeric IDs, keyboard focus,
scaling and accessible state labels remain requirements for the new flow.

The original icon, conceptual illustrations, cobalt/pale surfaces and consistent
native controls are retained. Schematics are original illustrations, not screenshots
of a Windows experiment. Updates download verified files but never install silently.

Current native Windows screenshots are recorded with source/run provenance. [Previews](previews/README.md)
explains regeneration and [the archived design research](previews/archive/v0.5.0/DESIGN.md)
retains the original design-award references and older interaction decisions. Those
references do not imply that ViVeUI won an award, and the old queued workflow is no
longer a description of this revision.
