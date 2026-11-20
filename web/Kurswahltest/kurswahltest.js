(function (root) {
  "use strict";

  const RULE_VERSION = "m2-2";
  const MINIMUM_COURSES = 42;
  const CORE_SUBJECTS = ["D", "M", "E", "BIO", "CH", "PH"];
  const SCIENCES = ["BIO", "CH", "PH"];
  const EXCLUDED_LF_SUBJECTS = ["INF", "I", "ETH", "ER", "KR"];
  const KNOWN_LANGUAGE_SUBJECTS = ["E", "F", "L", "GR", "I", "SPA", "RU", "POR"];
  const STEP_NAMES = ["Leistungsfächer", "Prüfungsfächer", "Pflichtbereiche", "Weitere Kurse", "Überblick"];
  const GENERAL_RULE_IDS = ["PROFILE-TRUST"];
  const STORAGE_KEY = "gsm-kurswahltest-v1";

  function ordinalCompare(a, b) {
    if (a === b) return 0;
    return a < b ? -1 : 1;
  }

  function distinct(arr) {
    return Array.from(new Set(arr));
  }

  function arraysEqual(a, b) {
    if (a.length !== b.length) return false;
    for (let i = 0; i < a.length; i++) {
      if (a[i] !== b[i]) return false;
    }
    return true;
  }

  function sortNumbers(arr) {
    return arr.slice().sort((a, b) => a - b);
  }

  function formatHoursDe(value) {
    const rounded = Math.round((value + Number.EPSILON) * 100) / 100;
    return String(rounded).replace(".", ",");
  }

  function normalizeCatalog(raw) {
    if (!raw || typeof raw !== "object") {
      throw new Error("Katalog fehlt.");
    }
    const subjects = (raw.subjects || []).map((s) => ({
      id: s.id,
      name: s.name,
      taskField: s.taskField !== undefined && s.taskField !== null ? s.taskField : null,
      isForeignLanguage: Boolean(s.isForeignLanguage),
      isScience: Boolean(s.isScience)
    }));
    const offers = (raw.offers || []).map((o) => ({
      id: o.id,
      subjectId: o.subjectId,
      type: o.type,
      variant: o.variant,
      status: o.status,
      schedule: (o.schedule || []).map((h) => ({
        semester: h.semester,
        weeklyHours: h.weeklyHours
      })),
      requirements: Array.isArray(o.requirements) ? o.requirements.slice() : [],
      cooperationSchool: o.cooperationSchool || "none",
      isTopUpCourse: Boolean(o.isTopUpCourse)
    }));
    return {
      schemaVersion: 3,
      version: raw.version,
      graduationYear: raw.graduationYear,
      isDemo: Boolean(raw.isDemo),
      submissionDeadline: raw.submissionDeadline || null,
      subjects,
      offers
    };
  }

  function createMissingProfile() {
    return {
      status: "missing",
      revision: null,
      issuedAt: null,
      detail: "",
      displayName: "",
      className: "",
      prerequisites: {
        italianProfile: "unknown",
        informaticsBridge: "unknown",
        informaticsImp: "unknown",
        continuousBilingual: "unknown",
        religionEthicsGrade10: null,
        sportExemptSemesters: null,
        continuedLanguages: [],
        religionEthicsPermission: "unknown",
        religionEthicsExamPermission: "unknown",
        permanentSportExemption: "unknown"
      }
    };
  }

  function createDefaultTestPrerequisites() {
    return {
      italianProfile: "no",
      informaticsBridge: "no",
      informaticsImp: "no",
      continuousBilingual: "no",
      religionEthicsGrade10: "ETH",
      sportExemptSemesters: [],
      continuedLanguages: ["E", "F"],
      religionEthicsPermission: "yes",
      religionEthicsExamPermission: "unknown",
      permanentSportExemption: "no"
    };
  }

  function createDefaultTestProfile() {
    return {
      status: "verified",
      revision: 1,
      issuedAt: new Date().toISOString(),
      detail: "Unverbindliche Selbstauskunft (Testversion für zu Hause)",
      displayName: "",
      className: "",
      prerequisites: createDefaultTestPrerequisites()
    };
  }

  function normalizePriorEducation(p) {
    const base = createDefaultTestPrerequisites();
    if (!p || typeof p !== "object") return base;
    const sport = Array.isArray(p.sportExemptSemesters)
      ? sortNumbers(distinct(p.sportExemptSemesters.filter((h) => Number.isInteger(h) && h >= 1 && h <= 4)))
      : (p.sportExemptSemesters === null ? null : []);
    const langs = Array.isArray(p.continuedLanguages)
      ? distinct(p.continuedLanguages.map((l) => String(l).trim()).filter(Boolean))
      : base.continuedLanguages;
    return {
      italianProfile: p.italianProfile || "unknown",
      informaticsBridge: p.informaticsBridge || "unknown",
      informaticsImp: p.informaticsImp || "unknown",
      continuousBilingual: p.continuousBilingual || "unknown",
      religionEthicsGrade10: p.religionEthicsGrade10 === "ER" || p.religionEthicsGrade10 === "KR" || p.religionEthicsGrade10 === "ETH"
        ? p.religionEthicsGrade10
        : null,
      sportExemptSemesters: sport,
      continuedLanguages: langs,
      religionEthicsPermission: p.religionEthicsPermission || "unknown",
      religionEthicsExamPermission: p.religionEthicsExamPermission || "unknown",
      permanentSportExemption: p.permanentSportExemption || "unknown"
    };
  }

  const CourseAnnotations = {
    label(offer) {
      if (!offer) return "";
      const parts = [];
      if (offer.cooperationSchool === "ewg") parts.push("Koopkurs am EWG");
      else if (offer.cooperationSchool === "gsm") parts.push("Koopkurs am GSM");
      if (offer.isTopUpCourse) parts.push("Aufsetzerkurs");
      return parts.join(" · ");
    },
    suffix(offer) {
      const label = CourseAnnotations.label(offer);
      return label.length > 0 ? " · " + label : "";
    }
  };

  function makeRule(id, description, source, scope, version, severity) {
    return { id, description, source, scope, version, severity };
  }

  function academicRule(id, description, source, severity = "invalid") {
    return makeRule(id, description, source, "GSM-Kurswahl; Rechtsstand für Abiturjahrgang vor Freigabe bestätigen", RULE_VERSION, severity);
  }

  const CatalogRules = {
    Remedy: "Angebotskonfiguration durch die Oberstufenberatung korrigieren lassen; Angebot bis dahin gesperrt.",
    Definitions: [
      makeRule("CATALOG-LF-SEMESTERS", "Leistungsfach-Angebot umfasst HJ 1–4", "Pflichtenheft §6 Nr.1,12", "Alle LF-Angebote", RULE_VERSION, "invalid"),
      makeRule("CATALOG-LF-EXCLUDED", "INF, I, ETH, ER, KR am GSM nicht als LF", "Pflichtenheft §4", "LF-Angebote", RULE_VERSION, "invalid"),
      makeRule("CATALOG-PSY", "Psychologie: Wahlfach, HJ 1–2, zweistündig", "Pflichtenheft §4", "PSY-Angebote", RULE_VERSION, "invalid"),
      makeRule("CATALOG-SIA", "SIA: Seminarkurs HJ 1–2, dreistündig", "Pflichtenheft §4", "SIA-Angebote", RULE_VERSION, "invalid"),
      makeRule("CATALOG-GEO-BF", "Geographie-BF nur HJ 3–4", "Pflichtenheft §4", "GEO-BF-Angebote", RULE_VERSION, "invalid"),
      makeRule("CATALOG-INF", "Informatik: BF HJ 1–4, dreistündig, mit Vorbildungsprüfung", "Pflichtenheft §4, §6 Nr.5", "INF-Angebote", RULE_VERSION, "invalid"),
      makeRule("CATALOG-I-PROFILE", "Italienisch nur mit Profilprüfung", "Pflichtenheft §4, §6 Nr.6", "I-Angebote", RULE_VERSION, "invalid"),
      makeRule("CATALOG-BILINGUAL", "Bilinguale Variante nur mit Bili-Vorbildungsprüfung", "Pflichtenheft §4, §6 Nr.7", "Bilinguale Angebote", RULE_VERSION, "invalid")
    ],
    checkOffer(offer) {
      const semesters = sortNumbers(offer.schedule.map((s) => s.semester));
      const messages = [];
      const requireRule = (condition, id, explanation) => {
        if (!condition) {
          messages.push({
            rule: CatalogRules.Definitions.find((d) => d.id === id),
            subjectIds: [offer.subjectId],
            semesters: semesters.slice(),
            explanation: `Angebot ${offer.id}: ${explanation}`,
            remedy: CatalogRules.Remedy
          });
        }
      };
      requireRule(offer.type !== "lf" || arraysEqual(semesters, [1, 2, 3, 4]),
        "CATALOG-LF-SEMESTERS", "Leistungsfächer benötigen HJ 1–4.");
      requireRule(offer.type !== "lf" || !EXCLUDED_LF_SUBJECTS.includes(offer.subjectId),
        "CATALOG-LF-EXCLUDED", "Dieses Fach wird am GSM nicht als Leistungsfach angeboten.");
      requireRule(offer.subjectId !== "PSY" || (offer.type === "elective" && arraysEqual(semesters, [1, 2]) && offer.schedule.every((s) => s.weeklyHours === 2)),
        "CATALOG-PSY", "Psychologie ist ein zweistündiges Wahlfach nur in HJ 1–2.");
      requireRule(offer.subjectId !== "SIA" || (offer.type === "seminar" && arraysEqual(semesters, [1, 2]) && offer.schedule.every((s) => s.weeklyHours === 3)),
        "CATALOG-SIA", "SIA ist ein dreistündiger Seminarkurs in HJ 1–2.");
      requireRule(offer.subjectId !== "GEO" || offer.type === "lf" || (offer.type === "bf" && arraysEqual(semesters, [3, 4])),
        "CATALOG-GEO-BF", "Geographie gibt es außerhalb des LF nur als BF in HJ 3–4 (keine Wahlkurse).");
      requireRule(offer.subjectId !== "INF" || (offer.type === "bf" && arraysEqual(semesters, [1, 2, 3, 4])
        && offer.schedule.every((s) => s.weeklyHours === 3) && offer.requirements.includes("informatics")),
      "CATALOG-INF", "Informatik benötigt BF HJ 1–4, drei Stunden und Vorbildungsprüfung.");
      requireRule(offer.subjectId !== "I" || ((offer.type === "bf" || offer.type === "lf") && arraysEqual(semesters, [1, 2, 3, 4]) && offer.requirements.includes("italian_profile")),
        "CATALOG-I-PROFILE", "Italienisch benötigt HJ 1–4 und die Profilprüfung.");
      requireRule(offer.variant !== "bilingual" || offer.requirements.includes("continuous_bilingual"),
        "CATALOG-BILINGUAL", "Bilinguale Angebote benötigen die Bili-Vorbildungsprüfung.");
      return messages;
    }
  };

  const AcademicRules = {
    Sciences: SCIENCES,
    GkContinuityExplanation: "Am GSM darf Gemeinschaftskunde bei LF Wirtschaft nicht nach einer Pause in HJ 2 wieder in HJ 3/4 belegt werden. Für GK in HJ 3/4 auch HJ 2 wählen.",
    Definitions: [
      academicRule("AGVO-LF", "Drei LF, davon zwei Kernfächer", "AGVO §12; Pflichtenheft §6 Nr.1"),
      academicRule("AGVO-COURSES", "12 LF und mindestens 30 weitere Kurse", "AGVO §§12,13; Pflichtenheft §6 Nr.2"),
      academicRule("AGVO-REQUIRED", "Pflichtfächer und GSM-Halbjahresplan", "AGVO §13; Pflichtenheft §§4,6 Nr.3,8"),
      academicRule("AGVO-LANGUAGE-SCIENCE", "Sprach- und Naturwissenschaftsbelegung", "AGVO §13; Pflichtenheft §6 Nr.4–6"),
      academicRule("AGVO-LANGUAGE-REVIEW", "Sprachlaufbahn oder INF-Alternative ungeklärt bzw. nicht bestätigt", "AGVO §13; Pflichtenheft §6 Nr.4–6", "needs_review"),
      academicRule("AGVO-SEMESTERS", "Belegung je Jahrgangsstufe", "AGVO §11 Abs.1 Satz 3; Pflichtenheft §6 Nr.12; Nutzerfestlegung 29.10.2026: kein Einzelhalbjahres-Sportersatz bei der Kurswahl"),
      academicRule("AGVO-RELIGION", "Religion/Ethik: Zustimmung, Wechsel und Feststellungsprüfung", "AGVO §§11,13 Abs.4,21 Abs.2 Nr.4/5; KOE 2026 Kap.3.3; Pflichtenheft §6 Nr.9", "needs_review"),
      academicRule("GSM-RELIGION-EXCLUSIVE", "Religion und Ethik nicht gleichzeitig", "Nutzerfestlegung 29.10.2026; Pflichtenheft §6 Nr.9"),
      makeRule("GSM-GK-CONTINUITY", "GK ohne Unterbrechung bei LF Wirtschaft", "GSM-Nutzerfestlegung 29.10.2026", "GSM; GK-BF bei LF Wirtschaft", "gsm-2026-10-29.1", "invalid"),
      academicRule("AGVO-SPORT", "Befreiung, LF-Eignung und Ersatzbedarf", "AGVO §13 Abs.5; Pflichtenheft §6 Nr.10", "needs_review"),
      academicRule("AGVO-SPORT-REPLACEMENT", "Zusätzliche Kurse statt Sport; kein Seminarkurs", "AGVO §13 Abs.5; ZSL Ratgeber Schulsport"),
      academicRule("AGVO-EXAM", "Mindestens eine mögliche Prüfungskombination", "AGVO §21; Pflichtenheft §6 Nr.11"),
      academicRule("PLAN-HOURS", "32 Wochenstunden als Planungshinweis", "Pflichtenheft §6 letzter Absatz", "needs_review"),
      academicRule("GSM-HOURS-UPPER", "Mehr als 37 Wochenstunden je Halbjahr", "Nutzerfestlegung 29.10.2026", "needs_review")
    ],
    isLanguageSubject(subject, catalog = null) {
      if (KNOWN_LANGUAGE_SUBJECTS.includes(subject)) return true;
      const found = catalog && catalog.subjects.find((x) => x.id === subject);
      return Boolean(found && found.isForeignLanguage);
    },
    languageCareerKnown(profile) {
      return profile.status === "verified" && Array.isArray(profile.prerequisites.continuedLanguages) && profile.prerequisites.continuedLanguages.length > 0;
    },
    languages(profile, catalog = null) {
      if (!AcademicRules.languageCareerKnown(profile)) return ["E", "F", "L"];
      return ["E", ...profile.prerequisites.continuedLanguages.filter((s) => s !== "E" && AcademicRules.isLanguageSubject(s, catalog))];
    },
    languageExcluded(subject, profile, catalog = null) {
      return subject !== "E" && subject !== "I"
        && AcademicRules.isLanguageSubject(subject, catalog)
        && AcademicRules.languageCareerKnown(profile)
        && !AcademicRules.languages(profile, catalog).includes(subject);
    },
    assessmentSubject(choice, profile) {
      if (profile.status !== "verified" || !profile.prerequisites.religionEthicsGrade10) return null;
      if (profile.prerequisites.religionEthicsExamPermission !== "unknown") return null;
      const grade10 = profile.prerequisites.religionEthicsGrade10;
      return choice.plannedOralSubjects.find((s) => (s === "ETH" || s === "ER" || s === "KR") && s !== grade10) || null;
    },
    scienceSubjects(catalog = null) {
      if (!catalog) return SCIENCES.slice();
      return distinct([...SCIENCES, ...catalog.subjects.filter((s) => s.isScience).map((s) => s.id)]);
    },
    four(selection) {
      return [1, 2, 3, 4].every((h) => selection.semesters.includes(h));
    },
    hasGkContinuityGap(choice) {
      return choice.selections.some((s) => s.type === "lf" && s.subjectId === "W")
        && choice.selections.some((s) => s.subjectId === "GK" && s.type === "bf"
          && s.semesters.some((h) => h === 3 || h === 4) && !s.semesters.includes(2));
    },
    mutuallyExclusiveSubjects(first, second) {
      return first !== second
        && (first === "ETH" || first === "ER" || first === "KR")
        && (second === "ETH" || second === "ER" || second === "KR");
    },
    requiresYearPair(choice, subject, firstSemester) {
      return !choice.selections.some((s) => s.type === "lf" && s.subjectId === "W")
        || !((subject === "GK" && firstSemester === 1) || (subject === "GEO" && firstSemester === 3));
    },
    semesterSelectionUnits(choice, offer) {
      if (offer.type === "lf" || offer.type === "seminar") {
        return [sortNumbers(offer.schedule.map((h) => h.semester))];
      }
      const units = [];
      for (const first of [1, 3]) {
        const pair = [first, first + 1];
        if (!pair.some((h) => offer.schedule.some((s) => s.semester === h))) continue;
        if (!AcademicRules.requiresYearPair(choice, offer.subjectId, first)) {
          for (const h of pair) {
            if (offer.schedule.some((s) => s.semester === h)) units.push([h]);
          }
        } else {
          units.push(pair);
        }
      }
      return units;
    },
    field(subject, profile, catalog = null) {
      const configured = catalog && catalog.subjects.find((s) => s.id === subject);
      if (configured && configured.taskField !== null && configured.taskField !== undefined) {
        return configured.taskField;
      }
      if (AcademicRules.languages(profile, catalog).includes(subject)) return 1;
      switch (subject) {
        case "D": case "BK": case "MU": return 1;
        case "G": case "GK": case "GEO": case "W": case "ETH": case "ER": case "KR": return 2;
        case "M": case "BIO": case "CH": case "PH": case "INF": return 3;
        default: return 0;
      }
    },
    check(choice, catalog, profile) {
      const messages = [];
      const add = (id, explanation, subjects = [], semesters = []) => {
        messages.push({
          rule: AcademicRules.Definitions.find((r) => r.id === id),
          subjectIds: subjects.slice(),
          semesters: semesters.slice(),
          explanation,
          remedy: "Belegung prüfen; offene Voraussetzungen und Sonderfälle mit der Oberstufenberatung klären."
        });
      };

      const lf = choice.selections.filter((s) => s.type === "lf");
      if (AcademicRules.hasGkContinuityGap(choice)) {
        add("GSM-GK-CONTINUITY", AcademicRules.GkContinuityExplanation, ["GK"], [2, 3, 4]);
      }

      const languages = AcademicRules.languages(profile, catalog);
      const sciences = AcademicRules.scienceSubjects(catalog);
      if (lf.length !== 3 || distinct(lf.map((s) => s.subjectId)).length !== 3
        || lf.filter((s) => s.subjectId === "D" || s.subjectId === "M" || sciences.includes(s.subjectId) || languages.includes(s.subjectId)).length < 2) {
        add("AGVO-LF", "Genau drei verschiedene LF, darunter mindestens zwei aus Deutsch, Mathematik, geeigneten fortgeführten Sprachen oder BIO/CH/PH wählen.");
      }

      const lfCourseSum = lf.reduce((acc, s) => acc + s.semesters.length, 0);
      const otherCourseSum = choice.selections.filter((s) => s.type !== "lf").reduce((acc, s) => acc + s.semesters.length, 0);
      if (lfCourseSum !== 12 || otherCourseSum < 30) {
        add("AGVO-COURSES", "Benötigt werden 12 LF-Halbjahreskurse und mindestens 30 weitere Kurse. SIA zählt nur seine zwei Halbjahre.");
      }

      for (const req of ChoiceWorkflow.required(choice, profile).filter((r) => r.subjectId !== "SP")) {
        if (!choice.selections.some((s) => s.subjectId === req.subjectId && req.semesters.every((h) => s.semesters.includes(h)))) {
          add("AGVO-REQUIRED", `${req.subjectId}: Pflichtbelegung fehlt.`, [req.subjectId], req.semesters);
        }
      }

      if (!choice.selections.some((s) => (s.subjectId === "BK" || s.subjectId === "MU") && AcademicRules.four(s))) {
        add("AGVO-REQUIRED", "Vier Kurse in einem der Fächer BK / MU fehlen.", ["BK", "MU"], [1, 2, 3, 4]);
      }

      const religionSemesters = new Set(
        choice.selections.filter((s) => s.subjectId === "ETH" || s.subjectId === "ER" || s.subjectId === "KR")
          .flatMap((s) => s.semesters)
      );
      if (![1, 2, 3, 4].every((h) => religionSemesters.has(h))) {
        add("AGVO-REQUIRED", "Vier Kurse in einem der Fächer ETH / ER / KR fehlen: Religion oder Ethik muss in allen vier Halbjahren belegt sein.",
          ["ETH", "ER", "KR"], [1, 2, 3, 4].filter((h) => !religionSemesters.has(h)));
      }

      if (profile.status === "verified" && !AcademicRules.languageCareerKnown(profile)
        && choice.selections.some((s) => s.subjectId === "F" || s.subjectId === "L")) {
        add("AGVO-LANGUAGE-REVIEW", "Für Französisch/Latein ist im Lehrerbestand noch keine Sprachlaufbahn bestätigt (Unterricht spätestens ab Klasse 8 nötig).",
          distinct(choice.selections.filter((s) => s.subjectId === "F" || s.subjectId === "L").map((s) => s.subjectId)));
      }

      const full = distinct(choice.selections.filter(AcademicRules.four).map((s) => s.subjectId));
      const scienceCount = full.filter((s) => sciences.includes(s)).length;
      const languageCount = full.filter((s) => languages.includes(s)).length;
      const inf = full.includes("INF") && catalog.offers.some((o) => o.subjectId === "INF"
        && choice.selections.some((s) => s.offerId === o.id)
        && ChoiceValidator.getAvailability(o, profile).canSelect);

      if (scienceCount < 1 || languageCount < 1 || !(languageCount >= 2 || scienceCount + (inf ? 1 : 0) >= 2)) {
        const unknownAlternative = scienceCount > 0 && (
          full.some((s) => {
            const subj = catalog.subjects.find((x) => x.id === s);
            const isLang = ["I", "F", "L", "GR", "SPA", "RU", "POR"].includes(s) || Boolean(subj && subj.isForeignLanguage);
            return isLang && !languages.includes(s) && !AcademicRules.languageExcluded(s, profile, catalog);
          }) || (full.includes("INF") && (profile.status !== "verified"
            || profile.prerequisites.informaticsImp === "unknown"
            || profile.prerequisites.informaticsBridge === "unknown"))
        );
        add(unknownAlternative ? "AGVO-LANGUAGE-REVIEW" : "AGVO-LANGUAGE-SCIENCE",
          "Eine geeignete Sprache und BIO/CH/PH sind nötig; zusätzlich zweite geeignete Sprache oder zweite Naturwissenschaft (berechtigtes INF als Alternative). Italienisch-Profil allein ist keine Sprachlaufbahnbestätigung.");
      }

      for (const s of choice.selections.filter((sel) => sel.type !== "lf" && sel.subjectId !== "SP")) {
        const oddPairs = [1, 3].filter((first) => AcademicRules.requiresYearPair(choice, s.subjectId, first)
          && s.semesters.includes(first) !== s.semesters.includes(first + 1)).length;
        if (oddPairs > 0) {
          add("AGVO-SEMESTERS",
            "HJ 1+2 bzw. HJ 3+4 zusammen wählen. Bei LF Wirtschaft sind GK HJ 1/2 und GEO HJ 3/4 einzeln möglich; zusätzliche GK-Kurse HJ 3+4 bleiben zusammen. Auch bei Sportbefreiung werden Ersatzkurse bei der Kurswahl als ganze Schuljahre gewählt.",
            [s.subjectId], s.semesters);
        }
      }

      const p = profile.status === "verified" ? profile.prerequisites : createMissingProfile().prerequisites;
      const chosenRelEthics = distinct(choice.selections.filter((s) => s.subjectId === "ETH" || s.subjectId === "ER" || s.subjectId === "KR").map((s) => s.subjectId));
      const conflictingSemesters = [1, 2, 3, 4].filter((h) => distinct(
        choice.selections.filter((s) => s.semesters.includes(h) && (s.subjectId === "ETH" || s.subjectId === "ER" || s.subjectId === "KR"))
          .map((s) => s.subjectId)
      ).length > 1);
      if (conflictingSemesters.length > 0) {
        add("GSM-RELIGION-EXCLUSIVE", "Ethik und Religion sowie verschiedene Religionsfächer dürfen nicht gleichzeitig belegt werden. Im Pflichtbereich genau ein Fach wählen.", chosenRelEthics, conflictingSemesters);
      }

      const relSubjects = distinct(
        choice.selections
          .filter((s) => (s.subjectId === "ETH" || s.subjectId === "ER" || s.subjectId === "KR") && s.semesters.length > 0)
          .slice()
          .sort((a, b) => Math.min(...a.semesters) - Math.min(...b.semesters))
          .map((s) => s.subjectId)
      );
      const grade10 = p.religionEthicsGrade10;
      const switchedRelEthics = Boolean(grade10) && chosenRelEthics.some((s) => s !== grade10);

      if (relSubjects.length > 1 && conflictingSemesters.length === 0) {
        add("AGVO-RELIGION", relSubjects.includes("ETH")
          ? `Wechsel während der Kursstufe (${relSubjects.join(" -> ")}): Die Abmeldung vom Religionsunterricht muss beantragt werden. Danach kann weder Religion noch Ethik mündliches Prüfungsfach sein (KOE 2026 Kap. 3.3); eine Feststellungsprüfung ist dafür nicht vorgesehen.`
          : `Wechsel des Religionsfachs während der Kursstufe (${relSubjects.join(" -> ")}): höchstens zwei Kurse einer anderen Religionsgemeinschaft und nur mit Zustimmung beider Religionsgemeinschaften (AGVO § 11 Abs. 3). Religion kann dann nicht mündliches Prüfungsfach sein.`,
        relSubjects);
      } else if ((grade10 === "KR" || grade10 === "ER") && chosenRelEthics.includes("ETH")) {
        add("AGVO-RELIGION",
          "Austritt aus Religionsunterricht muss beantragt werden. Für mündliche Prüfung in Ethik ist Feststellungsprüfung notwendig."
          + (AcademicRules.assessmentSubject(choice, profile) === "ETH" ? " Du planst Ethik mündlich: Die Feststellungsprüfung findet zu Beginn des 1. Halbjahres statt (AGVO § 21 Abs. 2 Nr. 5)." : "")
          + " Bitte mit der Oberstufenberatung klären.",
          ["ETH"]);
      } else if (switchedRelEthics) {
        add("AGVO-RELIGION",
          `Wechsel der Belegung gegenüber Klasse 10 (${grade10} -> ${chosenRelEthics.join("/")}): Für eine mündliche Prüfung im gewechselten Fach ist eine Feststellungsprüfung zu Beginn des 1. Schulhalbjahres erforderlich (AGVO § 21 Abs. 2 Nr. 4/5). Bitte mit der Oberstufenberatung klären.`,
          chosenRelEthics);
      } else if (chosenRelEthics.length > 0 && (p.religionEthicsPermission === "no"
        || (p.religionEthicsPermission === "unknown"
          && !((grade10 === "KR" || grade10 === "ER" || grade10 === "ETH") && chosenRelEthics.every((s) => s === grade10))))) {
        add("AGVO-RELIGION", "Konfession, Zustimmung oder Wechsel ist offen (für eine mündliche Prüfung ohne Vorbildung in Klasse 10 ist eine Überprüfung/Feststellungsprüfung erforderlich). BF Ethik bleibt grundsätzlich wählbar.", ["ER", "KR", "ETH"]);
      }

      if (!Array.isArray(p.sportExemptSemesters)) {
        add("AGVO-SPORT", "Sportbefreiung und Ersatzbedarf sind unbekannt.", ["SP"]);
      } else {
        const exemptions = p.sportExemptSemesters;
        const requiredSport = [1, 2, 3, 4].filter((h) => !exemptions.includes(h));
        if (requiredSport.some((h) => !choice.selections.some((s) => s.subjectId === "SP" && s.semesters.includes(h)))) {
          add("AGVO-REQUIRED", "Sportbelegung in nicht befreiten Halbjahren fehlt.", ["SP"], requiredSport);
        }
        if (exemptions.length > 0) {
          add("AGVO-SPORT", `${exemptions.length} Ersatzkurse nötig. Ersatzkurse als ganze Schuljahre (HJ 1+2 bzw. HJ 3+4) zusätzlich wählen; ein Kurs mehr als nötig schadet nicht. Umfang administrativ bestätigen; SIA ist kein Ersatz.`, ["SP"], exemptions);
          const totalWithoutSia = choice.selections.filter((s) => s.type !== "seminar").reduce((acc, s) => acc + s.semesters.length, 0);
          const extras = AcademicRules.maximumSportReplacementCourses(choice, profile);
          if (totalWithoutSia < 42 || extras < exemptions.length) {
            add("AGVO-SPORT-REPLACEMENT", `${exemptions.length} Ersatzkurse erforderlich (mindestens 42 Halbjahreskurse ohne SIA; aktuell ${totalWithoutSia} ohne SIA, höchstens ${extras} zusätzliche Basis-/Wahlfachkurse vorhanden). SIA ersetzt keine Sportkurse.`, ["SP"], exemptions);
          }
        }
      }

      if (lf.some((s) => s.subjectId === "SP") && p.permanentSportExemption !== "no") {
        add("AGVO-SPORT", "LF Sport: dauerhafte auch teilweise Befreiung bzw. fehlende Auskunft erfordert Klärung.", ["SP"]);
      }

      if (lf.length === 3 && AcademicRules.examOptions(choice, catalog, profile).length === 0) {
        add("AGVO-EXAM", "Die belegten Kurse ergeben keine mögliche mündliche Zweierkombination für Deutsch, Mathematik und alle Aufgabenfelder.");
      }

      const summaries = ChoiceValidator.summarize(choice, catalog);
      const overloaded = summaries.filter((s) => s.weeklyHours > 37);
      if (overloaded.length > 0) {
        add("GSM-HOURS-UPPER",
          "Mehr als 37 Unterrichtsstunden pro Woche werden in der Regel nicht genehmigt. "
          + overloaded.map((s) => `HJ ${s.semester}: ${s.weeklyHours} Wochenstunden`).join("; ")
          + ". Bitte reduziere deine Wahl oder besprich sie mit der Oberstufenberatung.",
          [], overloaded.map((s) => s.semester));
      }
      const avg = summaries.reduce((acc, s) => acc + s.weeklyHours, 0) / summaries.length;
      if (avg < 32) {
        add("PLAN-HOURS", `Durchschnitt ${formatHoursDe(avg)} Wochenstunden; Planungsrichtwert 32, kein automatischer Rechtsverstoß.`);
      }

      return messages;
    },
    maximumSportReplacementCourses(choice, profile) {
      const available = new Set();
      for (const s of choice.selections.filter((sel) => (sel.type === "bf" || sel.type === "elective") && sel.subjectId !== "SP")) {
        for (const h of s.semesters) available.add(`${s.subjectId}|${h}`);
      }
      const mandatory = new Set();
      for (const r of ChoiceWorkflow.required(choice, profile).filter((req) => req.subjectId !== "SP")) {
        for (const h of r.semesters) mandatory.add(`${r.subjectId}|${h}`);
      }
      const full = choice.selections.filter(AcademicRules.four).map((s) => s.subjectId);
      const arts = full.filter((s) => s === "BK" || s === "MU");
      if (arts.length === 0) arts.push("BK");
      const religions = full.filter((s) => s === "ETH" || s === "ER" || s === "KR");
      if (religions.length === 0) religions.push("ETH");
      const allowedLangs = AcademicRules.languages(profile);
      const languages = full.filter((s) => allowedLangs.includes(s));
      if (languages.length === 0) languages.push("E");
      const sciences = full.filter((s) => SCIENCES.includes(s));
      if (sciences.length === 0) sciences.push("BIO");

      let max = 0;
      for (const art of arts) {
        for (const religion of religions) {
          for (const language of languages) {
            for (const science of sciences) {
              const seconds = full.filter((s) => (allowedLangs.includes(s) && s !== language)
                || (SCIENCES.includes(s) && s !== science)
                || (s === "INF" && profile.status === "verified"
                  && (profile.prerequisites.informaticsBridge === "yes" || profile.prerequisites.informaticsImp === "yes")));
              if (seconds.length === 0) seconds.push("CH");
              for (const second of seconds) {
                const reserved = new Set(mandatory);
                for (const s of [art, religion, language, science, second]) {
                  for (const h of [1, 2, 3, 4]) reserved.add(`${s}|${h}`);
                }
                let count = 0;
                for (const item of available) {
                  if (!reserved.has(item)) count++;
                }
                if (count > max) max = count;
              }
            }
          }
        }
      }
      return max;
    },
    examOptions(choice, catalog, profile) {
      const lf = choice.selections.filter((s) => s.type === "lf").map((s) => s.subjectId);
      if (lf.length !== 3) return [];
      const candidates = ChoiceWorkflow.oralCandidates(choice, catalog, profile)
        .filter((id) => choice.selections.some((s) => s.subjectId === id && ChoiceWorkflow.oralSemesters(choice, id).every((h) => s.semesters.includes(h))))
        .slice()
        .sort(ordinalCompare);
      const options = [];
      for (let i = 0; i < candidates.length; i++) {
        for (let j = i + 1; j < candidates.length; j++) {
          if (AcademicRules.mutuallyExclusiveSubjects(candidates[i], candidates[j])) continue;
          const all = lf.concat([candidates[i], candidates[j]]);
          if (!all.includes("D") || !all.includes("M") || ![1, 2, 3].every((f) => all.some((s) => AcademicRules.field(s, profile, catalog) === f))) continue;
          const floor = AcademicRules.minimumCredits(choice, [candidates[i], candidates[j]], profile, catalog);
          if (floor > 40) continue;
          options.push({
            first: candidates[i],
            second: candidates[j],
            minimumCredits: floor,
            status: "needs_review",
            note: "Vorläufig; Rechtsstand, persönliche Prüfungsvoraussetzungen und Anrechnung bestätigen. BLL/SIA ersetzt kein Fach automatisch."
          });
        }
      }
      return options;
    },
    minimumCredits(choice, oral, profile, catalog = null) {
      const required = new Set();
      const add = (s, hList) => {
        for (const h of hList) required.add(`${s}|${h}`);
      };
      const four = [1, 2, 3, 4];
      for (const s of choice.selections.filter((sel) => sel.type === "lf")) add(s.subjectId, four);
      const choiceNoOral = { ...choice, plannedOralSubjects: [] };
      for (const r of ChoiceWorkflow.required(choiceNoOral, profile).filter((req) => ["D", "M", "G", "GK", "GEO"].includes(req.subjectId))) {
        add(r.subjectId, r.semesters);
      }
      for (const s of oral) add(s, ChoiceWorkflow.oralSemesters(choice, s));
      const artCandidates = choice.selections
        .filter((s) => s.subjectId === "BK" || s.subjectId === "MU")
        .slice()
        .sort((a, b) => ((b.type === "lf" || oral.includes(b.subjectId)) ? 1 : 0) - ((a.type === "lf" || oral.includes(a.subjectId)) ? 1 : 0));
      if (artCandidates.length > 0) add(artCandidates[0].subjectId, artCandidates[0].semesters.slice(0, 2));

      const allowedLanguages = AcademicRules.languages(profile, catalog);
      const allowedSciences = AcademicRules.scienceSubjects(catalog);
      const languages = distinct(choice.selections.filter((s) => AcademicRules.four(s) && allowedLanguages.includes(s.subjectId)).map((s) => s.subjectId));
      if (languages.length === 0) languages.push("__LANG1");
      const sciences = distinct(choice.selections.filter((s) => AcademicRules.four(s) && allowedSciences.includes(s.subjectId)).map((s) => s.subjectId));
      if (sciences.length === 0) sciences.push("__SCI1");

      const minima = [];
      for (const language of languages) {
        for (const science of sciences) {
          const seconds = languages.filter((s) => s !== language && s !== "__LANG1")
            .concat(sciences.filter((s) => s !== science && s !== "__SCI1"))
            .concat(choice.selections.filter((s) => s.subjectId === "INF" && AcademicRules.four(s)
              && profile.status === "verified"
              && (profile.prerequisites.informaticsImp === "yes" || profile.prerequisites.informaticsBridge === "yes")).map((s) => s.subjectId));
          if (seconds.length === 0) seconds.push("__BREADTH2");
          for (const second of seconds) {
            const set = new Set(required);
            for (const s of [language, science, second]) {
              for (const h of four) set.add(`${s}|${h}`);
            }
            minima.push(set.size);
          }
        }
      }
      return Math.min(...minima);
    }
  };

  function eitherKnowledge(a, b) {
    if (a === "yes" || b === "yes") return "yes";
    if (a === "no" && b === "no") return "no";
    return "unknown";
  }

  function religionSubjectUnknown(profile) {
    return profile.status !== "verified" || !["ER", "KR", "ETH"].includes(profile.prerequisites.religionEthicsGrade10);
  }

  const ChoiceValidator = {
    Version: RULE_VERSION,
    Definitions: [
      ...AcademicRules.Definitions,
      makeRule("GSM-RELIGION-CONFESSION", "Religionswahl nur passend zum bestätigten Klasse-10-Fach", "GSM-Nutzerfestlegung 11.11.2026", "ER/KR: Angebot, Belegung und Prüfungsplanung beider Programme", RULE_VERSION, "invalid"),
      makeRule("PROFILE-TRUST", "Vertrauenswürdiges Einzelprofil fehlt", "Pflichtenheft §5a", "Alle Schülerwahlen", RULE_VERSION, "needs_review"),
      makeRule("OFFER-VERSION", "Jahrgang und Angebots-/Regelversion", "Pflichtenheft §§4,8", "Alle Wahlen", RULE_VERSION, "invalid"),
      makeRule("OFFER-SELECTION", "Angebot, Variante und Halbjahre", "Pflichtenheft §§4,8", "Jede Belegung", RULE_VERSION, "invalid"),
      makeRule("SUBJECT-DUPLICATE", "Keine doppelte Fachbelegung", "Pflichtenheft §6 Nr.1,7", "Alle Fächer/Varianten", RULE_VERSION, "invalid"),
      makeRule("PREREQUISITE-NO", "Persönliche Voraussetzung nicht erfüllt", "Pflichtenheft §§4,5; AGVO §13 Abs.1 Nr.3", "INF/I/bilingual/Sprachlaufbahn", RULE_VERSION, "invalid"),
      makeRule("PREREQUISITE-UNKNOWN", "Persönliche Voraussetzung ungeklärt", "Pflichtenheft §§5,5a; GSM-Nutzerfestlegung 11.11.2026", "INF/I/bilingual/Religionswahl", RULE_VERSION, "needs_review"),
      ...CatalogRules.Definitions
    ],
    getAvailability(offer, profile) {
      const violation = CatalogRules.checkOffer(offer)[0] || null;
      if (violation) {
        return { canSelect: false, status: "invalid", explanation: `${violation.rule.id}: ${violation.explanation} Bitte an die Oberstufenberatung wenden.` };
      }
      if (offer.status !== "offered" && offer.status !== "established") {
        return { canSelect: false, status: "invalid", explanation: "Dieses Angebot ist geplant oder nicht zustande gekommen und nicht freigegeben." };
      }
      if (offer.subjectId === "SP" && offer.type === "lf"
        && (profile.status !== "verified" || profile.prerequisites.permanentSportExemption !== "no"
          || !Array.isArray(profile.prerequisites.sportExemptSemesters) || profile.prerequisites.sportExemptSemesters.length !== 0)) {
        return { canSelect: false, status: "needs_review", explanation: "LF Sport erfordert bestätigte Auskunft ohne dauerhafte oder teilweise Befreiung; Sonderfall mit Oberstufenberatung klären." };
      }
      if (AcademicRules.languageExcluded(offer.subjectId, profile)) {
        return {
          canSelect: false,
          status: "invalid",
          explanation: `Nicht in deiner bestätigten Sprachlaufbahn (${AcademicRules.languages(profile).join(", ")}). Kurse in einer Fremdsprache setzen Unterricht spätestens ab Klasse 8 voraus (AGVO § 13 Abs. 1 Nr. 3). Stimmt die Angabe nicht, bitte an die Oberstufenberatung wenden.`
        };
      }
      if (offer.subjectId === "ER" || offer.subjectId === "KR") {
        if (religionSubjectUnknown(profile)) {
          return { canSelect: true, status: "needs_review", explanation: "Religion/Ethik-Fach aus Klasse 10 ist unbekannt oder nicht bestätigt. Beide Religionsfächer und Ethik bleiben vorläufig wählbar; bitte mit der Oberstufenberatung klären." };
        }
        if (profile.prerequisites.religionEthicsGrade10 !== offer.subjectId) {
          return { canSelect: false, status: "invalid", explanation: "Religion ist nur passend zum bestätigten Fach aus Klasse 10 wählbar (ER: evangelisch, KR: katholisch). Ethik bleibt wählbar. Stimmt die Profilangabe nicht, bitte an die Oberstufenberatung wenden." };
        }
      }
      if (offer.requirements.length === 0) {
        return { canSelect: true, status: "needs_review", explanation: "Vorläufig wählbar; fachliche Freigabe für den Jahrgang steht aus." };
      }
      if (profile.status !== "verified") {
        return { canSelect: false, status: "needs_review", explanation: "Persönliche Voraussetzung ungeprüft. Bitte an die Oberstufenberatung wenden." };
      }
      const states = offer.requirements.map((r) => {
        switch (r) {
          case "italian_profile": return profile.prerequisites.italianProfile;
          case "continuous_bilingual": return profile.prerequisites.continuousBilingual;
          case "informatics": return eitherKnowledge(profile.prerequisites.informaticsBridge, profile.prerequisites.informaticsImp);
          default: return "unknown";
        }
      });
      if (states.includes("no")) return { canSelect: false, status: "invalid", explanation: "Persönliche Voraussetzung nicht erfüllt." };
      if (states.includes("unknown")) return { canSelect: false, status: "needs_review", explanation: "Voraussetzung fachlich zu klären; Oberstufenberatung kontaktieren." };
      return { canSelect: true, status: "needs_review", explanation: "Vorbildung erfüllt; vollständige Rechtsprüfung steht aus." };
    },
    validate(choice, catalog, profile) {
      const messages = [...AcademicRules.check(choice, catalog, profile)];
      const add = (id, explanation, remedy, subject = null, semesters = []) => {
        messages.push({
          rule: ChoiceValidator.AllDefinitions().find((d) => d.id === id),
          subjectIds: subject === null ? [] : [subject],
          semesters: semesters.slice(),
          explanation,
          remedy
        });
      };
      if (profile.status !== "verified") {
        add("PROFILE-TRUST", "Kein verifiziertes persönliches Profil; Profilstatus: " + profile.status, "Oberstufenberatung kontaktieren. Entwurf bleibt vorläufig.");
      }
      if (choice.graduationYear !== catalog.graduationYear || choice.offerVersion !== catalog.version || choice.ruleVersion !== ChoiceValidator.Version) {
        add("OFFER-VERSION", "Jahrgang, Angebotsversion oder Regelversion weicht ab.", "Vorhandenen Entwurf erhalten und Version durch Oberstufenberatung klären lassen.");
      }
      for (const selection of choice.selections) {
        const offer = catalog.offers.find((o) => o.id === selection.offerId) || null;
        const catalogViolations = offer ? CatalogRules.checkOffer(offer) : [];
        for (const violation of catalogViolations) {
          messages.push({ ...violation, semesters: selection.semesters.slice() });
        }
        if (!offer || offer.subjectId !== selection.subjectId || offer.type !== selection.type || offer.variant !== selection.variant
          || selection.semesters.some((s) => !offer.schedule.some((h) => h.semester === s))
          || (offer.type === "lf" && selection.semesters.length !== 4)
          || (offer.type === "seminar" && !arraysEqual(sortNumbers(selection.semesters), sortNumbers(offer.schedule.map((h) => h.semester))))) {
          add("OFFER-SELECTION", "Belegung passt nicht zum konfigurierten Angebot.", "Fach, Kursart, Variante und Halbjahre kontrollieren.", selection.subjectId, selection.semesters);
          continue;
        }
        if (catalogViolations.length > 0) continue;
        const availability = ChoiceValidator.getAvailability(offer, profile);
        if (availability.canSelect && (offer.subjectId === "ER" || offer.subjectId === "KR") && religionSubjectUnknown(profile)) {
          add("PREREQUISITE-UNKNOWN", availability.explanation, "Klasse-10-Fach durch die Oberstufenberatung bestätigen lassen.", selection.subjectId, selection.semesters);
        }
        if (!availability.canSelect) {
          const ruleId = (offer.status !== "offered" && offer.status !== "established") ? "OFFER-SELECTION"
            : availability.status !== "invalid" ? "PREREQUISITE-UNKNOWN"
            : (offer.subjectId === "ER" || offer.subjectId === "KR") ? "GSM-RELIGION-CONFESSION" : "PREREQUISITE-NO";
          add(ruleId, availability.explanation, "Angebot/Vorbildung mit der Oberstufenberatung klären.", selection.subjectId, selection.semesters);
        }
      }
      const oralCandidates = ChoiceWorkflow.oralCandidates(choice, catalog, profile);
      if (choice.plannedOralSubjects.length !== 2
        || choice.plannedOralSubjects.some((s) => !oralCandidates.includes(s))
        || ChoiceWorkflow.oralError(choice, catalog, profile) !== null) {
        const switchedRelOral = choice.plannedOralSubjects.find((s) => (s === "ETH" || s === "ER" || s === "KR")
          && profile.status === "verified" && profile.prerequisites.religionEthicsGrade10
          && profile.prerequisites.religionEthicsGrade10 !== s
          && profile.prerequisites.religionEthicsExamPermission === "no");
        const explanation = switchedRelOral
          ? `${switchedRelOral} wurde in Klasse 10 nicht besucht (${profile.prerequisites.religionEthicsGrade10}): Laut Oberstufenberatung ist es nicht als mündliches Prüfungsfach möglich (AGVO § 21 Abs. 2 Nr. 4/5).`
          : (ChoiceWorkflow.oralError(choice, catalog, profile) || "Mündliche Prüfungsplanung passt nicht zu LF, Angebot oder persönlichen Voraussetzungen.");
        messages.push({
          rule: ChoiceValidator.AllDefinitions().find((d) => d.id === "PLAN-ORAL"),
          subjectIds: choice.plannedOralSubjects.slice(),
          semesters: [],
          explanation,
          remedy: "Zwei zulässige Basisfächer für die mündliche Prüfung planen."
        });
      }
      const countsBySubject = new Map();
      for (const s of choice.selections) {
        countsBySubject.set(s.subjectId, (countsBySubject.get(s.subjectId) || 0) + 1);
      }
      for (const [subjectId, count] of countsBySubject.entries()) {
        if (count > 1) {
          add("SUBJECT-DUPLICATE", "Dasselbe Fach wurde mehrfach als Kursart oder Variante gewählt.", "Eine eindeutige Fachbelegung wählen; Sonderfälle administrativ klären.", subjectId);
        }
      }
      const status = messages.some((m) => m.rule.severity === "invalid") ? "invalid"
        : messages.length > 0 ? "needs_review" : "valid";
      return { status, messages };
    },
    summarize(choice, catalog) {
      return [1, 2, 3, 4].map((semester) => {
        const selected = choice.selections.filter((s) => s.semesters.includes(semester));
        const weeklyHours = selected.reduce((sum, s) => {
          const offer = catalog.offers.find((o) => o.id === s.offerId);
          const entry = offer && offer.schedule.find((h) => h.semester === semester);
          return sum + (entry ? entry.weeklyHours : 0);
        }, 0);
        return { semester, courseCount: selected.length, weeklyHours };
      });
    },
    AllDefinitions() {
      return [...ChoiceValidator.Definitions, ...ChoiceWorkflow.Definitions];
    }
  };

  const ChoiceWorkflow = {
    MinimumCourses: MINIMUM_COURSES,
    Definitions: [
      makeRule("PLAN-LF", "Drei LF mit mindestens zwei Kernfächern", "Pflichtenheft §6 Nr.1; Nutzerablauf 25.10.2026", "Demo-Wahlassistent", RULE_VERSION, "invalid"),
      makeRule("PLAN-ORAL", "Zwei mündliche Planungsfächer; D/M und Aufgabenfelder", "Pflichtenheft §6 Nr.8,11", "Demo-Prüfungsplanung, keine Zulassung", RULE_VERSION, "invalid"),
      makeRule("PLAN-REQUIRED", "Eindeutige Pflichtbelegung ergänzen, Alternativen offenlassen", "Pflichtenheft §4; §6 Nr.3,4,8,10", "Demo-Belegungsplanung", RULE_VERSION, "needs_review"),
      makeRule("PLAN-AREA-ART", "Kunst oder Musik mit vier Kursen planen", "Pflichtenheft §6 Nr.3", "Demo-Belegungsplanung", RULE_VERSION, "needs_review"),
      makeRule("PLAN-AREA-RELIGION", "Religion oder Ethik mit vier Kursen planen; Zulässigkeit offen", "Pflichtenheft §6 Nr.3,9", "Demo-Belegungsplanung", RULE_VERSION, "needs_review"),
      makeRule("PLAN-AREA-LANGUAGE", "Mindestens eine fortgeführte Fremdsprache", "Pflichtenheft §6 Nr.3,4,6", "GSM-Belegungsplanung", RULE_VERSION, "needs_review"),
      makeRule("PLAN-AREA-SCIENCE", "Mindestens eine Naturwissenschaft (BIO/CH/PH)", "Pflichtenheft §6 Nr.3,4,5", "GSM-Belegungsplanung", RULE_VERSION, "needs_review"),
      makeRule("PLAN-AREA-BREADTH", "Zweite Sprache oder zweite Naturwissenschaft", "Pflichtenheft §6 Nr.4–6", "Persönlich bestätigte Sprachlaufbahnen", RULE_VERSION, "needs_review"),
      makeRule("PLAN-MIN-COURSES", "Planungsziel mindestens 42 Halbjahreskurse", "Pflichtenheft §6 Nr.2", "Demo-Belegungsplanung, keine Anrechnung", RULE_VERSION, "needs_review")
    ],
    _lf(choice) {
      return choice.selections.filter((s) => s.type === "lf").map((s) => s.subjectId);
    },
    forcedOral(choice) {
      const lf = ChoiceWorkflow._lf(choice);
      return ["D", "M"].filter((s) => !lf.includes(s));
    },
    oralSemesters() {
      return [1, 2, 3, 4];
    },
    oralCandidates(choice, catalog, profile = null) {
      const ctx = profile || createMissingProfile();
      const lf = ChoiceWorkflow._lf(choice);
      const ids = catalog.offers.filter((o) => o.type === "bf" && o.subjectId !== "GEO"
        && (AcademicRules.field(o.subjectId, ctx, catalog) !== 0 || o.subjectId === "SP")
        && !lf.includes(o.subjectId)
        && ChoiceValidator.getAvailability(o, ctx).canSelect
        && !(o.subjectId === "SP" && ctx.status === "verified"
          && (ctx.prerequisites.permanentSportExemption === "yes" || (Array.isArray(ctx.prerequisites.sportExemptSemesters) && ctx.prerequisites.sportExemptSemesters.length > 0)))
        && !((o.subjectId === "ETH" || o.subjectId === "ER" || o.subjectId === "KR") && ctx.status === "verified"
          && ctx.prerequisites.religionEthicsExamPermission === "no")
        && ChoiceWorkflow.oralSemesters(choice, o.subjectId).every((h) => o.schedule.some((s) => s.semester === h)))
        .map((o) => o.subjectId);
      return distinct(ids);
    },
    _combination(choice, oral, profile = null, catalog = null) {
      const ctx = profile || createMissingProfile();
      const all = ChoiceWorkflow._lf(choice).concat(oral);
      return !all.some((a) => all.some((b) => AcademicRules.mutuallyExclusiveSubjects(a, b)))
        && all.includes("D") && all.includes("M")
        && [1, 2, 3].every((f) => all.some((s) => AcademicRules.field(s, ctx, catalog) === f));
    },
    lfError(choice, catalog, profile = null) {
      const context = profile || createMissingProfile();
      const lf = ChoiceWorkflow._lf(choice);
      const sciences = AcademicRules.scienceSubjects(catalog);
      if (lf.length !== 3 || distinct(lf).length !== 3) return "PLAN-LF: Bitte genau drei verschiedene Leistungsfächer wählen.";
      if (lf.some((s) => AcademicRules.field(s, context, catalog) === 0 && s !== "SP")) return "PLAN-LF: needs_review – Fachzuordnung außerhalb des bekannten Demoangebots klären.";
      if (lf.filter((s) => CORE_SUBJECTS.includes(s) || sciences.includes(s) || AcademicRules.languages(context, catalog).includes(s)).length < 2) {
        return "PLAN-LF: Mindestens zwei LF aus Deutsch, Mathematik, fortgeführten Fremdsprachen oder BIO/CH/PH wählen.";
      }
      const badLf = choice.selections.filter((s) => s.type === "lf").some((s) => s.semesters.length !== 4
        || !catalog.offers.some((o) => o.id === s.offerId && o.subjectId === s.subjectId && o.type === s.type
          && o.variant === s.variant && ChoiceValidator.getAvailability(o, context).canSelect));
      if (badLf) return "PLAN-LF: Angebot oder Belegung der Leistungsfächer korrigieren.";
      const candidates = ChoiceWorkflow.oralCandidates(choice, catalog, context);
      if (!candidates.some((a) => candidates.some((b) => a !== b && ChoiceWorkflow._combination(choice, [a, b], context, catalog)))) {
        return "PLAN-ORAL: Mit diesen LF ist im verfügbaren Demoangebot keine Kombination für Deutsch, Mathematik und alle Aufgabenfelder möglich.";
      }
      return null;
    },
    oralError(choice, catalog, profile = null) {
      const oral = choice.plannedOralSubjects;
      if (oral.some((a) => oral.some((b) => AcademicRules.mutuallyExclusiveSubjects(a, b)))) {
        return "GSM-RELIGION-EXCLUSIVE: Nur eines der Fächer Ethik, evangelische oder katholische Religion als mündliches Prüfungsfach planen.";
      }
      if (oral.length !== 2 || distinct(oral).length !== 2) return "PLAN-ORAL: Zwei verschiedene mündliche Prüfungsfächer planen.";
      const candidates = ChoiceWorkflow.oralCandidates(choice, catalog, profile);
      if (oral.some((s) => !candidates.includes(s))) return "PLAN-ORAL: Prüfungsplanung passt nicht mehr zu LF oder Angebot. Bitte neu wählen.";
      if (!ChoiceWorkflow._combination(choice, oral, profile, catalog)) return "PLAN-ORAL: Deutsch, Mathematik und alle drei Aufgabenfelder müssen abgedeckt sein.";
      return null;
    },
    required(choice, profile = null) {
      const lf = ChoiceWorkflow._lf(choice);
      const map = new Map();
      for (const subject of ["D", "M", "G", "SP"]) map.set(subject, [1, 2, 3, 4]);
      if (profile && profile.status === "verified" && Array.isArray(profile.prerequisites.sportExemptSemesters)) {
        const ex = profile.prerequisites.sportExemptSemesters;
        map.set("SP", [1, 2, 3, 4].filter((h) => !ex.includes(h)));
      }
      map.set("GK", lf.includes("W") ? [1] : [1, 2]);
      map.set("GEO", lf.includes("W") ? [3] : [3, 4]);
      for (const subject of choice.plannedOralSubjects) {
        if (!(subject === "SP" && profile && profile.status === "verified"
          && Array.isArray(profile.prerequisites.sportExemptSemesters) && profile.prerequisites.sportExemptSemesters.length > 0)) {
          map.set(subject, ChoiceWorkflow.oralSemesters(choice, subject));
        }
      }
      const res = [];
      for (const [subjectId, semesters] of map.entries()) {
        if (!lf.includes(subjectId) && semesters.length > 0) {
          res.push({ subjectId, semesters });
        }
      }
      return res;
    },
    usefulOralCandidates(choice, catalog, profile = null) {
      const candidates = ChoiceWorkflow.oralCandidates(choice, catalog, profile);
      const planned = choice.plannedOralSubjects.filter((s) => candidates.includes(s));
      if (planned.length >= 2) return [];
      return candidates.filter((c) => !planned.includes(c) && (planned.length === 1
        ? ChoiceWorkflow._combination(choice, [planned[0], c], profile, catalog)
        : candidates.some((d) => d !== c && ChoiceWorkflow._combination(choice, [c, d], profile, catalog))));
    },
    countedCourses(choice) {
      return choice.selections.reduce((sum, s) => sum + s.semesters.length, 0);
    },
    tasks(choice, catalog, profile) {
      const requiredSubjects = new Set(ChoiceWorkflow.required(choice, profile).map((r) => r.subjectId));
      const four = [1, 2, 3, 4];
      const def = (id) => ChoiceWorkflow.Definitions.find((d) => d.id === id);

      const area = (ruleId, title, hint, subjects, needed) => {
        const lf = choice.selections.filter((s) => s.type === "lf" && subjects.includes(s.subjectId)).map((s) => s.subjectId);
        const choose = Math.max(0, needed - lf.length);
        const matching = choice.selections
          .filter((s) => s.type !== "lf" && subjects.includes(s.subjectId) && four.every((h) => s.semesters.includes(h))
            && catalog.offers.some((o) => o.id === s.offerId))
          .slice()
          .sort((a, b) => {
            const reqDiff = (requiredSubjects.has(b.subjectId) ? 1 : 0) - (requiredSubjects.has(a.subjectId) ? 1 : 0);
            if (reqDiff !== 0) return reqDiff;
            return (SCIENCES.includes(b.subjectId) ? 1 : 0) - (SCIENCES.includes(a.subjectId) ? 1 : 0);
          })
          .slice(0, choose);
        const chosenOfferIds = matching.map((s) => s.offerId);
        const candidates = catalog.offers.filter((o) => o.type !== "lf" && subjects.includes(o.subjectId)
          && !lf.includes(o.subjectId) && four.every((h) => o.schedule.some((sh) => sh.semester === h)));
        const optionOfferIds = candidates.filter((o) => ChoiceValidator.getAvailability(o, profile).canSelect).map((o) => o.id);
        const restricted = candidates.filter((o) => !optionOfferIds.includes(o.id))
          .map((o) => ({ offerId: o.id, explanation: ChoiceValidator.getAvailability(o, profile).explanation }));
        const have = lf.concat(matching.filter((s) => optionOfferIds.includes(s.offerId)).map((s) => s.subjectId));
        return {
          id: ruleId,
          rule: def(ruleId),
          title,
          hint,
          semesters: four.slice(),
          choose,
          open: Math.max(0, needed - have.length),
          optionOfferIds,
          chosenOfferIds,
          fulfilledBySubjects: lf,
          restricted,
          get isSatisfied() { return this.open === 0; },
          get isExclusive() { return this.choose === 1; }
        };
      };

      const sciences = AcademicRules.scienceSubjects(catalog);
      const languages = AcademicRules.languages(profile, catalog);
      const tasks = [
        area("PLAN-AREA-ART", "Kunst oder Musik", "Wähle eines der beiden Fächer für HJ 1–4.", ["BK", "MU"], 1),
        area("PLAN-AREA-RELIGION", "Religion oder Ethik",
          "Wähle ein Fach für HJ 1–4. Religion wird passend zum bestätigten Klasse-10-Fach angeboten (ER: evangelisch, KR: katholisch). Bei unbekannten oder ungeprüften Angaben bleiben beide Religionsfächer vorläufig wählbar; bitte mit der Oberstufenberatung klären. Ethik bleibt wählbar; Wechsel und mündliche Prüfung ggf. klären.",
          ["ER", "KR", "ETH"], 1),
        area("PLAN-AREA-LANGUAGE", "Mindestens eine Fremdsprache",
          "Englisch, Französisch oder Latein für HJ 1–4. Eine passende LF-Belegung erfüllt die Pflicht bereits. Italienisch nur mit bestätigter geeigneter Sprachlaufbahn.",
          languages, 1),
        area("PLAN-AREA-SCIENCE", "Mindestens eine Naturwissenschaft",
          "Biologie, Chemie oder Physik für HJ 1–4. Informatik ersetzt diese erste Naturwissenschaft nicht.",
          sciences, 1)
      ];

      const full = choice.selections.filter(AcademicRules.four).slice().sort((a, b) => {
        const lfDiff = (b.type === "lf" ? 1 : 0) - (a.type === "lf" ? 1 : 0);
        if (lfDiff !== 0) return lfDiff;
        return (requiredSubjects.has(b.subjectId) ? 1 : 0) - (requiredSubjects.has(a.subjectId) ? 1 : 0);
      });
      const langTask = tasks.find((t) => t.id === "PLAN-AREA-LANGUAGE");
      const firstLanguage = langTask
        ? langTask.fulfilledBySubjects.concat(langTask.chosenOfferIds.map((id) => choice.selections.find((s) => s.offerId === id).subjectId))[0] || null
        : null;
      const sciTask = tasks.find((t) => t.id === "PLAN-AREA-SCIENCE");
      const firstScience = sciTask
        ? sciTask.fulfilledBySubjects.concat(sciTask.chosenOfferIds.map((id) => choice.selections.find((s) => s.offerId === id).subjectId))[0] || null
        : null;

      const secondary = full.find((s) => s.subjectId !== firstLanguage && s.subjectId !== firstScience
        && (languages.includes(s.subjectId) || sciences.includes(s.subjectId) || s.subjectId === "INF")
        && catalog.offers.some((o) => o.id === s.offerId && ChoiceValidator.getAvailability(o, profile).canSelect)) || null;
      const satisfied = secondary !== null && (secondary.subjectId !== "INF" || firstScience !== null);
      const candidateSubjects = distinct([
        ...languages,
        ...sciences,
        "INF",
        "I",
        ...catalog.subjects.filter((s) => s.isForeignLanguage).map((s) => s.id)
      ]);
      const lfSubjects = ChoiceWorkflow._lf(choice);
      const candidates = catalog.offers.filter((o) => o.type === "bf"
        && candidateSubjects.includes(o.subjectId)
        && o.subjectId !== firstLanguage
        && o.subjectId !== firstScience
        && !lfSubjects.includes(o.subjectId)
        && four.every((h) => o.schedule.some((sh) => sh.semester === h)));
      const canSelectBreadth = (offer) => ChoiceValidator.getAvailability(offer, profile).canSelect
        && (offer.subjectId !== "I" || languages.includes("I"));

      tasks.push({
        id: "PLAN-AREA-BREADTH",
        rule: def("PLAN-AREA-BREADTH"),
        title: "Zweite Fremdsprache oder zweite Naturwissenschaft",
        hint: "Wähle zusätzlich eine andere Fremdsprache oder Naturwissenschaft für HJ 1–4. Berechtigtes Informatik kann die zweite Naturwissenschaft ersetzen. Beide Wege sind gleichwertige Alternativen.",
        semesters: four.slice(),
        choose: secondary && secondary.type === "lf" ? 0 : 1,
        open: satisfied ? 0 : 1,
        optionOfferIds: candidates.filter(canSelectBreadth).map((o) => o.id),
        chosenOfferIds: secondary && secondary.type !== "lf" ? [secondary.offerId] : [],
        fulfilledBySubjects: secondary && secondary.type === "lf" ? [secondary.subjectId] : [],
        restricted: candidates.filter((o) => !canSelectBreadth(o)).map((o) => ({
          offerId: o.id,
          explanation: o.subjectId === "I" && !languages.includes("I")
            ? "Geeignete fortgeführte Italienisch-Sprachlaufbahn muss im Profil bestätigt werden."
            : ChoiceValidator.getAvailability(o, profile).explanation
        })),
        get isSatisfied() { return this.open === 0; },
        get isExclusive() { return this.choose === 1; }
      });

      const reqList = ChoiceWorkflow.required(choice, profile).slice().reverse();
      for (const req of reqList) {
        if (choice.selections.some((s) => s.subjectId === req.subjectId)) continue;
        const variants = catalog.offers.filter((o) => o.subjectId === req.subjectId && o.type === "bf"
          && ChoiceValidator.getAvailability(o, profile).canSelect
          && req.semesters.every((h) => o.schedule.some((sh) => sh.semester === h)));
        if (variants.length < 2) continue;
        const name = catalog.subjects.find((s) => s.id === req.subjectId).name;
        tasks.unshift({
          id: "PLAN-VARIANT-" + req.subjectId,
          rule: def("PLAN-REQUIRED"),
          title: name + ": Kursvariante wählen",
          hint: "Dieses Fach ist erforderlich. Wähle ausdrücklich die normale oder bilinguale Kursvariante.",
          semesters: req.semesters.slice(),
          choose: 1,
          open: 1,
          optionOfferIds: variants.map((o) => o.id),
          chosenOfferIds: [],
          fulfilledBySubjects: [],
          restricted: [],
          get isSatisfied() { return this.open === 0; },
          get isExclusive() { return this.choose === 1; }
        });
      }
      return tasks;
    },
    remaining(choice, catalog, profile) {
      const notes = [];
      for (const req of ChoiceWorkflow.required(choice, profile)) {
        if (!choice.selections.some((s) => s.subjectId === req.subjectId && req.semesters.every((h) => s.semesters.includes(h)))) {
          notes.push(`PLAN-REQUIRED: ${req.subjectId} benötigt HJ ${req.semesters.join(", ")}; Belegung ergänzen oder Angebot klären.`);
        }
      }
      for (const task of ChoiceWorkflow.tasks(choice, catalog, profile).filter((t) => !t.isSatisfied)) {
        notes.push(`${task.rule.id}: ${task.title} – noch ${task.open} offen.`);
      }
      const count = ChoiceWorkflow.countedCourses(choice);
      if (count < MINIMUM_COURSES) {
        notes.push(`PLAN-MIN-COURSES: ${count} von mindestens ${MINIMUM_COURSES} Halbjahreskursen (12 LF + 30 weitere) geplant.`);
      }
      notes.push("Die Oberstufenberatung klärt die in den Prüfhinweisen genannten offenen Fragen. Der Regelstand für deinen Abiturjahrgang und die endgültige Anrechnung müssen fachlich bestätigt werden.");
      return notes;
    }
  };

  function createSession(rawCatalog, options = {}) {
    const catalog = normalizeCatalog(rawCatalog);
    const allowSelfServicePrerequisites = options.allowSelfServicePrerequisites !== undefined
      ? Boolean(options.allowSelfServicePrerequisites)
      : true;
    const firstStep = allowSelfServicePrerequisites ? 0 : 1;
    const lastStep = 5;
    let profile = options.profile || (allowSelfServicePrerequisites ? createDefaultTestProfile() : createMissingProfile());
    const nowIso = new Date().toISOString();
    let choice = {
      schemaVersion: 2,
      documentId: options.documentId || "00000000-0000-4000-8000-000000000001",
      studentId: options.studentId || "demo-testversion",
      graduationYear: catalog.graduationYear,
      offerVersion: catalog.version,
      ruleVersion: RULE_VERSION,
      revision: 1,
      createdAt: nowIso,
      updatedAt: nowIso,
      submissionStatus: "draft",
      phase: "preliminary",
      selections: [],
      plannedOralSubjects: []
    };

    const displacedBasics = new Map();
    let lfChangeSubject = null;
    let step = firstStep;
    let isDirty = false;
    let hasSavedFile = false;
    let message = "Neuer vorläufiger Entwurf – noch nicht gespeichert.";
    let automaticMessage = "";
    let lfChangeMessage = "";
    const listeners = [];

    function emit() {
      for (const fn of listeners) fn();
    }

    function subjectName(subjectId) {
      const subj = catalog.subjects.find((s) => s.id === subjectId);
      return subj ? subj.name : subjectId;
    }

    function availability(offer) {
      return ChoiceValidator.getAvailability(offer, profile);
    }

    function applyRequired() {
      let selections = choice.selections.slice();
      const notes = [];
      for (const req of ChoiceWorkflow.required(choice, profile)) {
        const existing = selections.find((s) => s.subjectId === req.subjectId) || null;
        const offers = catalog.offers.filter((o) => o.subjectId === req.subjectId && o.type === "bf"
          && availability(o).canSelect && req.semesters.every((h) => o.schedule.some((sh) => sh.semester === h)));
        const offer = existing === null ? (offers.length === 1 ? offers[0] : null) : (offers.find((o) => o.id === existing.offerId) || null);
        if (offer === null) {
          notes.push(`${subjectName(req.subjectId)} (${req.subjectId}): needs_review – kein eindeutiges verfügbares BF; vorhandene Wahl bleibt erhalten.`);
          continue;
        }
        const semesters = sortNumbers(distinct([...(existing ? existing.semesters : []), ...req.semesters]));
        const replaced = selections.filter((s) => AcademicRules.mutuallyExclusiveSubjects(s.subjectId, req.subjectId)
          && s.semesters.some((h) => semesters.includes(h)));
        for (const prev of replaced) {
          notes.push(`${subjectName(prev.subjectId)} wurde durch ${subjectName(req.subjectId)} ersetzt, weil du es mündlich planst. Religion und Ethik können nicht gleichzeitig belegt werden.`);
        }
        const replacedIds = new Set(replaced.map((r) => r.offerId));
        selections = selections.filter((s) => !replacedIds.has(s.offerId));
        if (existing && arraysEqual(existing.semesters, semesters)) continue;
        selections = selections.filter((s) => s.offerId !== offer.id);
        selections.push({
          offerId: offer.id,
          subjectId: offer.subjectId,
          type: offer.type,
          variant: offer.variant,
          semesters
        });
      }
      const changed = JSON.stringify(selections) !== JSON.stringify(choice.selections);
      if (changed) {
        choice = { ...choice, selections, updatedAt: new Date().toISOString() };
        isDirty = true;
      }
      automaticMessage = notes.join("\n");
    }

    const session = {
      get catalog() { return catalog; },
      get profile() { return profile; },
      get allowSelfServicePrerequisites() { return allowSelfServicePrerequisites; },
      get firstStep() { return firstStep; },
      get lastStep() { return lastStep; },
      get choice() { return choice; },
      get step() { return step; },
      get isDirty() { return isDirty; },
      get hasSavedFile() { return hasSavedFile; },
      get message() { return message; },
      get automaticMessage() { return automaticMessage; },
      get lfChangeMessage() { return lfChangeMessage; },
      get validation() { return ChoiceValidator.validate(choice, catalog, profile); },
      get summary() { return ChoiceValidator.summarize(choice, catalog); },
      get stepError() {
        return step === 1 ? ChoiceWorkflow.lfError(choice, catalog, profile)
          : step === 2 ? ChoiceWorkflow.oralError(choice, catalog, profile) : null;
      },
      get oralCandidates() { return ChoiceWorkflow.oralCandidates(choice, catalog, profile); },
      get forcedOral() { return ChoiceWorkflow.forcedOral(choice); },
      get usefulOralCandidates() { return ChoiceWorkflow.usefulOralCandidates(choice, catalog, profile); },
      get planningNotes() { return ChoiceWorkflow.remaining(choice, catalog, profile); },
      get tasks() { return ChoiceWorkflow.tasks(choice, catalog, profile); },
      get countedCourses() { return ChoiceWorkflow.countedCourses(choice); },
      get examOptions() { return AcademicRules.examOptions(choice, catalog, profile); },
      get lfOffers() { return catalog.offers.filter((o) => o.type === "lf" && availability(o).canSelect); },
      get extraOffers() {
        return catalog.offers.filter((o) => o.type !== "lf" && availability(o).canSelect
          && !choice.selections.some((s) => {
            const existing = catalog.offers.find((x) => x.id === s.offerId);
            return (s.subjectId === o.subjectId && existing && availability(existing).canSelect)
              || AcademicRules.mutuallyExclusiveSubjects(s.subjectId, o.subjectId);
          }));
      },
      get profileStatusText() {
        if (profile.status === "verified") {
          const dt = profile.issuedAt ? new Date(profile.issuedAt) : new Date();
          const pad = (n) => String(n).padStart(2, "0");
          const stamp = `${pad(dt.getDate())}.${pad(dt.getMonth() + 1)}.${dt.getFullYear()} ${pad(dt.getHours())}:${pad(dt.getMinutes())}`;
          return `Profil geprüft · Revision ${profile.revision} · Stand ${stamp} · ${profile.detail}`;
        }
        if (profile.status === "profile_invalid") {
          return "Profil ungültig (profile_invalid) – bitte an die Oberstufenberatung wenden. " + profile.detail;
        }
        return "Kein geprüftes persönliches Profil – alle Angaben ungeprüft (needs_review). " + profile.detail;
      },
      get prerequisites() {
        const p = profile.prerequisites;
        const verified = profile.status === "verified";
        const val = (k) => !verified ? "ungeprüft" : k === "yes" ? "ja" : k === "no" ? "nein" : "ungeprüft";
        const offersText = (req, fallback) => {
          const names = distinct(catalog.offers.filter((o) => o.requirements.includes(req))
            .map((o) => subjectName(o.subjectId) + (o.type === "lf" ? " LF" : "") + (o.variant === "bilingual" ? " bilingual" : "")));
          return names.length === 0 ? fallback : "Nötig für: " + names.join(", ") + ".";
        };
        return [
          {
            id: "INF",
            label: "Informatik: Brückenkurs Klasse 10 / IMP",
            value: `${val(p.informaticsBridge)} / ${val(p.informaticsImp)}`,
            effect: offersText("informatics", "Im Angebot nicht vorhanden."),
            isVerified: verified
          },
          {
            id: "I",
            label: "Italienisch als dritte Fremdsprache (Profil)",
            value: verified
              ? `${val(p.italianProfile)}${!p.continuedLanguages || p.continuedLanguages.length === 0 ? "" : ` · bestätigte Sprachlaufbahn: ${p.continuedLanguages.join(", ")}`}`
              : "ungeprüft",
            effect: offersText("italian_profile", "Im Angebot nicht vorhanden."),
            isVerified: verified
          },
          {
            id: "BILI",
            label: "Durchgängig bilingualer Bildungsgang",
            value: val(p.continuousBilingual),
            effect: offersText("continuous_bilingual", "Im Angebot nicht vorhanden."),
            isVerified: verified
          },
          {
            id: "REL",
            label: "Religion/Ethik in Klasse 10",
            value: verified && p.religionEthicsGrade10
              ? `${subjectName(p.religionEthicsGrade10)} (Belegung/Prüfung: ${val(p.religionEthicsPermission)} / ${val(p.religionEthicsExamPermission)})`
              : "ungeprüft",
            effect: "Bestimmt die angebotene Religion (ER: evangelisch, KR: katholisch). Bei unbekannten oder ungeprüften Angaben sind beide Religionsfächer vorläufig wählbar; bitte mit der Oberstufenberatung klären. Ethik bleibt wählbar; bei Wechsel kann für die mündliche Prüfung eine Feststellungsprüfung nötig sein.",
            isVerified: verified
          },
          {
            id: "SP",
            label: "Sportbefreiung",
            value: verified && Array.isArray(p.sportExemptSemesters)
              ? `${p.sportExemptSemesters.length === 0 ? "keine" : "HJ " + p.sportExemptSemesters.join(", ")} (dauerhaft: ${val(p.permanentSportExemption)})`
              : "ungeprüft",
            effect: "Bei Befreiung sind Ersatzkurse nötig und Sport entfällt als LF/mündliches Prüfungsfach; Prüfung durch die Oberstufenberatung.",
            isVerified: verified
          }
        ];
      },
      get plan() {
        const tasks = ChoiceWorkflow.tasks(choice, catalog, profile);
        const required = ChoiceWorkflow.required(choice, profile);
        const messages = ChoiceValidator.validate(choice, catalog, profile).messages;
        const issue = (subject) => {
          const relevant = messages.filter((m) => m.subjectIds.includes(subject));
          const status = relevant.some((m) => m.rule.severity === "invalid") ? "invalid"
            : relevant.length > 0 ? "needs_review" : null;
          const text = relevant.length === 0 ? null : relevant.map((m) => `${m.rule.id}: ${m.explanation}\n${m.remedy}`).join("\n\n");
          return { status, text };
        };
        const orderOf = (s) => {
          if (s.type === "lf") return 0;
          if (required.some((r) => r.subjectId === s.subjectId)) return 1;
          if (tasks.some((t) => t.chosenOfferIds.includes(s.offerId))) return 2;
          return 3;
        };
        const offerIndex = new Map(catalog.offers.map((o, idx) => [o.id, idx]));
        const rows = choice.selections.slice().sort((a, b) => {
          const ordDiff = orderOf(a) - orderOf(b);
          if (ordDiff !== 0) return ordDiff;
          return (offerIndex.has(a.offerId) ? offerIndex.get(a.offerId) : -1)
            - (offerIndex.has(b.offerId) ? offerIndex.get(b.offerId) : -1);
        }).map((s) => {
          const iss = issue(s.subjectId);
          const ord = orderOf(s);
          return {
            label: subjectName(s.subjectId),
            offerId: s.offerId,
            type: s.type,
            variant: s.variant,
            semesters: s.semesters.slice(),
            isAutomatic: ord === 1,
            isOral: choice.plannedOralSubjects.includes(s.subjectId),
            issue: iss.status,
            isOpen: false,
            step: ord === 0 ? 1 : ord === 3 ? 4 : 3,
            issueText: iss.text
          };
        });
        if (step >= 3) {
          for (const r of required.filter((req) => !choice.selections.some((s) => s.subjectId === req.subjectId))) {
            rows.push({
              label: subjectName(r.subjectId) + " – Angebot klären",
              offerId: null,
              type: null,
              variant: "normal",
              semesters: r.semesters.slice(),
              isAutomatic: true,
              isOral: false,
              issue: "needs_review",
              isOpen: true,
              step: 3,
              issueText: `PLAN-REQUIRED: ${subjectName(r.subjectId)} benötigt HJ ${r.semesters.join(", ")}; kein eindeutiges verfügbares BF im Angebot.`
            });
          }
        }
        for (const t of tasks.filter((task) => !task.isSatisfied)) {
          rows.push({
            label: t.title,
            offerId: null,
            type: null,
            variant: "normal",
            semesters: t.semesters.slice(),
            isAutomatic: false,
            isOral: false,
            issue: null,
            isOpen: true,
            step: 3,
            issueText: t.hint
          });
        }
        return rows;
      },
      onChanged(fn) {
        listeners.push(fn);
        return () => {
          const idx = listeners.indexOf(fn);
          if (idx >= 0) listeners.splice(idx, 1);
        };
      },
      subjectName,
      availability,
      restrictedOffers(lf) {
        return catalog.offers.filter((o) => (o.type === "lf") === lf
          && !availability(o).canSelect
          && !choice.selections.some((s) => s.offerId === o.id));
      },
      lockedSemesters(offerId) {
        const selection = choice.selections.find((s) => s.offerId === offerId);
        if (!selection) return [];
        const offer = catalog.offers.find((o) => o.id === offerId);
        if (!offer || !availability(offer).canSelect) return [];
        if (selection.type === "lf") return selection.semesters.slice();
        const locked = ChoiceWorkflow.required(choice, profile)
          .filter((r) => r.subjectId === selection.subjectId)
          .flatMap((r) => r.semesters);
        const task = ChoiceWorkflow.tasks(choice, catalog, profile).find((t) => t.chosenOfferIds.includes(offerId));
        if (task) locked.push(...task.semesters);
        return sortNumbers(distinct(locked));
      },
      isRequiredSubject(subjectId) {
        return ChoiceWorkflow.required(choice, profile).some((r) => r.subjectId === subjectId);
      },
      semesterSelectionUnits(offerId) {
        const offer = catalog.offers.find((o) => o.id === offerId);
        return AcademicRules.semesterSelectionUnits(choice, offer, profile);
      },
      selectedTaskOptions(task) {
        const combined = distinct([...task.optionOfferIds, ...task.chosenOfferIds]);
        return combined.filter((id) => choice.selections.some((s) => s.offerId === id && task.semesters.every((h) => s.semesters.includes(h))));
      },
      replacesTaskAnswer(task) {
        const selected = session.selectedTaskOptions(task);
        return task.isExclusive && selected.length <= 1
          && !selected.some((id) => session.isRequiredSubject(catalog.offers.find((o) => o.id === id).subjectId));
      },
      setTestPrerequisites(prerequisites) {
        if (!allowSelfServicePrerequisites) {
          throw new Error("Vorbildungsangaben können nur in der Testversion geändert werden.");
        }
        const normalized = normalizePriorEducation(prerequisites);
        profile = {
          status: "verified",
          revision: (profile.revision || 0) + 1,
          issuedAt: new Date().toISOString(),
          detail: "Unverbindliche Selbstauskunft (Testversion für zu Hause – keine schulische Bestätigung)",
          displayName: "",
          className: "",
          prerequisites: normalized
        };
        emit();
      },
      previousStep() {
        if (step <= firstStep) return;
        step--;
        emit();
      },
      goToStep(target) {
        if (target < firstStep || target >= step) return;
        step = target;
        emit();
      },
      nextStep() {
        if (step === 0) {
          step = 1;
          emit();
          return;
        }
        const err = session.stepError;
        if (err) throw new Error(err);
        if (step === 1) {
          const forced = ChoiceWorkflow.forcedOral(choice);
          const candidates = ChoiceWorkflow.oralCandidates(choice, catalog, profile);
          const oral = distinct([...forced, ...choice.plannedOralSubjects.filter((s) => candidates.includes(s))]).slice(0, 2);
          if (!arraysEqual(oral, choice.plannedOralSubjects)) {
            choice = { ...choice, plannedOralSubjects: oral, updatedAt: new Date().toISOString() };
            isDirty = true;
          }
        }
        if (step === 2) {
          applyRequired();
          lfChangeMessage = "";
          lfChangeSubject = null;
        }
        step = Math.min(lastStep, step + 1);
        if (isDirty) message = "Ungespeicherte Änderungen – vorläufig, nicht eingereicht.";
        emit();
      },
      setOral(subject, selected) {
        if (step !== 2) throw new Error("Mündliche Planung ist in Schritt 2 möglich.");
        if (!selected && ChoiceWorkflow.forcedOral(choice).includes(subject)) {
          throw new Error("Deutsch/Mathematik ist ohne entsprechendes LF mündlich erforderlich.");
        }
        let oral = choice.plannedOralSubjects.filter((s) => s !== subject);
        if (selected) {
          if (!ChoiceWorkflow.oralCandidates(choice, catalog, profile).includes(subject)) {
            throw new Error("Fach ist für diese Planung nicht verfügbar.");
          }
          if (oral.length === 2) throw new Error("Zuerst ein mündliches Fach abwählen.");
          oral = [...oral, subject];
        }
        choice = { ...choice, plannedOralSubjects: oral, updatedAt: new Date().toISOString() };
        isDirty = true;
        message = "Ungespeicherte Änderungen – vorläufig, nicht eingereicht.";
        emit();
      },
      setSelection(offerId, semesters) {
        const offer = catalog.offers.find((o) => o.id === offerId);
        if (!offer) throw new Error("Unbekanntes Angebot: " + offerId);
        const selected = sortNumbers(distinct(semesters));
        const proposed = {
          ...choice,
          selections: [
            ...choice.selections.filter((s) => s.offerId !== offerId),
            { offerId: offer.id, subjectId: offer.subjectId, type: offer.type, variant: offer.variant, semesters: selected }
          ]
        };
        if (offer.subjectId === "GK" && AcademicRules.hasGkContinuityGap(proposed)) {
          throw new Error(AcademicRules.GkContinuityExplanation);
        }
        if (selected.length > 0) {
          const avail = availability(offer);
          if (!avail.canSelect) throw new Error(avail.explanation);
          if (selected.length !== semesters.length || selected.some((s) => !offer.schedule.some((h) => h.semester === s))) {
            throw new Error("Halbjahr wird für dieses Angebot nicht angeboten.");
          }
          if (offer.type === "lf" && selected.length !== 4) {
            throw new Error("Leistungsfächer werden für alle vier Halbjahre gewählt.");
          }
          if (offer.type === "lf" && choice.selections.filter((s) => s.type === "lf" && s.offerId !== offerId).length >= 3) {
            throw new Error("Es sind bereits drei Leistungsfächer gewählt. Zum Tauschen zuerst ein Leistungsfach abwählen.");
          }
          if (offer.type === "seminar" && !arraysEqual(selected, sortNumbers(offer.schedule.map((h) => h.semester)))) {
            throw new Error("Seminarkurse werden über alle angebotenen Halbjahre gewählt.");
          }
          if (choice.selections.some((s) => AcademicRules.mutuallyExclusiveSubjects(s.subjectId, offer.subjectId)
            && s.semesters.some((h) => selected.includes(h)))) {
            throw new Error("Religion und Ethik sowie verschiedene Religionsfächer können nicht gleichzeitig belegt werden. Wechsle das Fach im Schritt Pflichtbereiche; ein mündlich geplantes Fach zuerst in Prüfungsfächer ändern.");
          }
          if (choice.selections.some((s) => {
            if (s.offerId === offerId || s.subjectId !== offer.subjectId) return false;
            if (offer.type === "lf" && s.type === "bf") return false;
            const existingOffer = catalog.offers.find((o) => o.id === s.offerId);
            return existingOffer && availability(existingOffer).canSelect;
          })) {
            throw new Error("Dieses Fach ist bereits gewählt. Vor einem Wechsel die bisherige Kursart/Variante abwählen.");
          }
        }
        const replacedUnavailable = selected.length > 0
          ? choice.selections.find((s) => {
            if (s.offerId === offerId || s.subjectId !== offer.subjectId) return false;
            if (offer.type === "lf" && s.type === "bf") return false;
            const existingOffer = catalog.offers.find((o) => o.id === s.offerId);
            return !existingOffer || !availability(existingOffer).canSelect;
          }) || null
          : null;
        const replacedBasic = selected.length > 0 && offer.type === "lf"
          ? choice.selections.find((s) => s.subjectId === offer.subjectId && s.type === "bf") || null
          : null;
        const replacesBasic = replacedBasic !== null;
        const removesLf = selected.length === 0 && offer.type === "lf" && choice.selections.some((s) => s.offerId === offerId);
        let restored = null;
        if (removesLf && displacedBasics.has(offer.subjectId)) {
          const displaced = displacedBasics.get(offer.subjectId);
          displacedBasics.delete(offer.subjectId);
          const basicOffer = catalog.offers.find((o) => o.id === displaced.basic.offerId);
          if (basicOffer && availability(basicOffer).canSelect
            && !choice.selections.some((s) => s.offerId !== offerId && (s.subjectId === offer.subjectId
              || (AcademicRules.mutuallyExclusiveSubjects(s.subjectId, offer.subjectId) && s.semesters.some((h) => displaced.basic.semesters.includes(h)))))) {
            restored = displaced;
          }
        }
        let selections = choice.selections.filter((s) => s.offerId !== offerId
          && (!replacedUnavailable || s.offerId !== replacedUnavailable.offerId)
          && !(replacesBasic && s.subjectId === offer.subjectId && s.type === "bf"));
        if (selected.length > 0) {
          selections.push({ offerId: offer.id, subjectId: offer.subjectId, type: offer.type, variant: offer.variant, semesters: selected });
        }
        if (restored !== null) {
          selections.push({ ...restored.basic, semesters: restored.basic.semesters.slice() });
        }
        let oral = (selected.length > 0 && offer.type === "lf")
          ? choice.plannedOralSubjects.filter((s) => s !== offer.subjectId)
          : choice.plannedOralSubjects.slice();
        if (restored && restored.wasOral && oral.length < 2 && !oral.includes(offer.subjectId)) {
          oral.push(offer.subjectId);
        }
        if (replacedBasic !== null) {
          displacedBasics.set(offer.subjectId, {
            basic: { ...replacedBasic, semesters: replacedBasic.semesters.slice() },
            wasOral: choice.plannedOralSubjects.includes(offer.subjectId)
          });
        } else if (selected.length > 0 && offer.type !== "lf") {
          displacedBasics.delete(offer.subjectId);
        }
        choice = { ...choice, selections, plannedOralSubjects: oral, updatedAt: new Date().toISOString() };
        if (replacesBasic) {
          lfChangeMessage = `${subjectName(offer.subjectId)} ist jetzt Leistungsfach. Die bisherige Basisfachbelegung wurde automatisch entfernt. Bitte die Prüfungsfächer und die weiteren Schritte erneut durchgehen.`;
          lfChangeSubject = offer.subjectId;
        } else if (restored !== null) {
          lfChangeMessage = `${subjectName(offer.subjectId)} ist kein Leistungsfach mehr. Die vorherige Basisfachbelegung (HJ ${restored.basic.semesters.join(", ")}) wurde wiederhergestellt`
            + (oral.includes(offer.subjectId) && restored.wasOral ? ", ebenso die mündliche Planung" : "")
            + ". Bitte die Prüfungsfächer und die weiteren Schritte erneut durchgehen.";
          lfChangeSubject = offer.subjectId;
        } else if (removesLf && lfChangeSubject === offer.subjectId) {
          lfChangeMessage = "";
          lfChangeSubject = null;
        }
        isDirty = true;
        message = "Ungespeicherte Änderungen – vorläufig, nicht eingereicht.";
        emit();
      },
      chooseTaskOption(taskId, offerId, selected) {
        const task = session.tasks.find((t) => t.id === taskId);
        const offer = catalog.offers.find((o) => o.id === offerId);
        if (!selected) {
          if (session.isRequiredSubject(offer.subjectId)) {
            throw new Error("Dieses Fach ist durch deine Prüfungsplanung oder als Pflichtfach gesetzt.");
          }
          session.setSelection(offerId, []);
          return;
        }
        if (!task.optionOfferIds.includes(offerId)) {
          throw new Error("Dieses Angebot passt nicht zu dieser Frage.");
        }
        const existingSel = choice.selections.find((s) => s.offerId === offerId);
        const existing = existingSel ? existingSel.semesters : [];
        if (task.semesters.every((h) => existing.includes(h))) return;
        const snapshot = JSON.parse(JSON.stringify(choice));
        try {
          if (session.replacesTaskAnswer(task)) {
            for (const prev of session.selectedTaskOptions(task).filter((id) => id !== offerId)) {
              session.setSelection(prev, []);
            }
          } else if (!task.isExclusive && task.chosenOfferIds.length >= task.choose) {
            throw new Error("Zuerst eine gewählte Option abwählen oder weitere Kurse in Schritt 4 ergänzen.");
          }
          session.setSelection(offerId, distinct([...existing, ...task.semesters]));
          if (task.rule.id === "PLAN-REQUIRED") {
            applyRequired();
            emit();
          }
        } catch (err) {
          if (JSON.stringify(choice) !== JSON.stringify(snapshot)) {
            choice = snapshot;
            emit();
          }
          throw err;
        }
      },
      exportState() {
        return {
          schemaVersion: 1,
          savedAt: new Date().toISOString(),
          choice,
          prerequisites: profile.prerequisites
        };
      },
      saveToBrowser(storage) {
        if (!storage) return;
        if (!isDirty && hasSavedFile) return;
        const nextRev = hasSavedFile ? choice.revision + 1 : choice.revision;
        choice = { ...choice, revision: nextRev, updatedAt: new Date().toISOString() };
        storage.setItem(STORAGE_KEY, JSON.stringify(session.exportState()));
        hasSavedFile = true;
        isDirty = false;
        message = `Entwurf im Browser gespeichert (Revision ${choice.revision}) – unverbindlicher Probelauf.`;
        emit();
      },
      loadFromBrowser(storage) {
        if (!storage) return false;
        const raw = storage.getItem(STORAGE_KEY);
        if (!raw) {
          message = "Kein gespeicherter Entwurf im Browser vorhanden.";
          emit();
          return false;
        }
        const parsed = JSON.parse(raw);
        if (!parsed || !parsed.choice || parsed.choice.graduationYear !== catalog.graduationYear) {
          throw new Error("Gespeicherter Entwurf passt nicht zu diesem Fachangebot.");
        }
        if (allowSelfServicePrerequisites && parsed.prerequisites) {
          profile = {
            ...createDefaultTestProfile(),
            revision: (profile.revision || 0) + 1,
            prerequisites: normalizePriorEducation(parsed.prerequisites)
          };
        }
        choice = {
          ...parsed.choice,
          offerVersion: catalog.version,
          ruleVersion: RULE_VERSION
        };
        step = firstStep;
        automaticMessage = "";
        lfChangeMessage = "";
        lfChangeSubject = null;
        displacedBasics.clear();
        hasSavedFile = true;
        isDirty = false;
        message = `Entwurf aus dem Browser geladen (Revision ${choice.revision}).`;
        emit();
        return true;
      },
      resetDraft(storage) {
        if (storage) storage.removeItem(STORAGE_KEY);
        const now = new Date().toISOString();
        profile = allowSelfServicePrerequisites ? createDefaultTestProfile() : createMissingProfile();
        choice = {
          schemaVersion: 2,
          documentId: choice.documentId,
          studentId: choice.studentId,
          graduationYear: catalog.graduationYear,
          offerVersion: catalog.version,
          ruleVersion: RULE_VERSION,
          revision: 1,
          createdAt: now,
          updatedAt: now,
          submissionStatus: "draft",
          phase: "preliminary",
          selections: [],
          plannedOralSubjects: []
        };
        displacedBasics.clear();
        lfChangeSubject = null;
        automaticMessage = "";
        lfChangeMessage = "";
        step = firstStep;
        isDirty = false;
        hasSavedFile = false;
        message = "Neuer vorläufiger Entwurf – noch nicht gespeichert.";
        emit();
      }
    };

    return session;
  }

  function displayText(value) {
    return String(value || "")
      .replaceAll("needs_review", "durch die Oberstufenberatung zu klären")
      .replaceAll("profile_invalid", "Profil ungültig");
  }

  function plainText(text, catalog) {
    let out = String(text || "");
    const colon = out.indexOf(": ");
    if (colon > 0 && colon < 28 && /^[A-Z0-9-]+$/.test(out.slice(0, colon))) {
      out = out.slice(colon + 2);
    }
    const sortedSubjects = catalog.subjects.slice().sort((a, b) => b.id.length - a.id.length);
    for (const subject of sortedSubjects) {
      if (out.startsWith(subject.id + " ") || out.startsWith(subject.id + ":")) {
        out = subject.name + out.slice(subject.id.length);
        break;
      }
    }
    return out;
  }

  function formatSemesters(semesters) {
    const list = sortNumbers(semesters);
    if (list.length > 2 && list[list.length - 1] - list[0] === list.length - 1) {
      return `${list[0]}–${list[list.length - 1]}`;
    }
    return list.join(", ");
  }

  function formatPrintCourseType(type) {
    return type === "lf" ? "Leistungsfach" : type === "bf" ? "Basisfach" : type === "elective" ? "Wahlfach" : "Seminarkurs";
  }

  function formatPrintSemesters(semesters) {
    const ordered = sortNumbers(distinct(semesters));
    if (ordered.length === 0) return "";
    if (ordered.length === 1) return "HJ " + ordered[0];
    if (ordered.length === 2 && ordered[1] === ordered[0] + 1) return `HJ ${ordered[0]} und ${ordered[1]}`;
    if (ordered.length >= 3 && ordered[ordered.length - 1] - ordered[0] === ordered.length - 1) {
      return `HJ ${ordered[0]}–${ordered[ordered.length - 1]}`;
    }
    return "HJ " + ordered.join(", ");
  }

  function printSubjectName(catalog, subjectId) {
    const found = catalog.subjects.find((s) => s.id === subjectId);
    if (found) return found.name;
    const fallback = {
      D: "Deutsch", M: "Mathematik", E: "Englisch", F: "Französisch", L: "Latein", I: "Italienisch",
      BIO: "Biologie", CH: "Chemie", PH: "Physik", G: "Geschichte", GK: "Gemeinschaftskunde", GEO: "Geographie",
      W: "Wirtschaft", BK: "Bildende Kunst", MU: "Musik", ETH: "Ethik", ER: "Evangelische Religion",
      KR: "Katholische Religion", SP: "Sport", INF: "Informatik", PSY: "Psychologie", SIA: "Schüler-Ingenieur-Akademie (SIA)"
    };
    return fallback[subjectId] || subjectId;
  }

  function sanitizePrintText(text, catalog) {
    let value = String(text || "").trim().replace(/^(?:(?:AGVO|PLAN|GSM|CATALOG|PROFILE|OFFER|SUBJECT|PREREQUISITE)-[A-Z0-9-]+:\s*)+/, "");
    const sortedSubjects = catalog.subjects.slice().sort((a, b) => b.id.length - a.id.length);
    for (const subject of sortedSubjects) {
      if (value.startsWith(subject.id + ": ")) {
        value = subject.name + ": " + value.slice(subject.id.length + 2);
        break;
      }
      if (value.startsWith(subject.id + " ")) {
        value = subject.name + value.slice(subject.id.length);
        break;
      }
    }
    value = displayText(value)
      .replaceAll("BIO/CH/PH", "Biologie, Chemie oder Physik")
      .replaceAll("BK / MU", "Bildende Kunst oder Musik")
      .replaceAll("ETH / ER / KR", "Ethik, Evangelische Religion oder Katholische Religion")
      .replaceAll("ER / KR / ETH", "Evangelische Religion, Katholische Religion oder Ethik")
      .replaceAll("Profilstatus: Missing", "persönliches Profil fehlt")
      .replaceAll("Profilstatus: ProfileInvalid", "persönliches Profil ungültig")
      .replaceAll("Profilstatus: Unverified", "persönliches Profil ungeprüft")
      .replaceAll("Profilstatus: Verified", "persönliches Profil geprüft")
      .replaceAll(" -> ", " → ");
    for (const id of ["ETH", "ER", "KR", "GEO", "GK", "INF"]) {
      const fullName = printSubjectName(catalog, id);
      value = value.replace(new RegExp(`(?<![A-Za-zÄÖÜäöüß])${id}(?![A-Za-zÄÖÜäöüß])`, "g"), fullName);
    }
    value = value
      .replace(/(?<!Schüler-Ingenieur-Akademie \()(?<![A-Za-zÄÖÜäöüß])SIA(?![A-Za-zÄÖÜäöüß])(?!\))/g, "Schüler-Ingenieur-Akademie (SIA)")
      .replaceAll("LF-Halbjahreskurse", "Leistungsfach-Halbjahreskurse")
      .replaceAll("LF-Belegung", "Leistungsfach-Belegung")
      .replaceAll("LF-Eignung", "Leistungsfach-Eignung")
      .replaceAll("INF-Alternative", "Informatik-Alternative")
      .replaceAll("GK-Kurse", "Gemeinschaftskunde-Kurse")
      .replace(/(?<![A-Za-zÄÖÜäöüß-])LF(?![A-Za-zÄÖÜäöüß-])/g, "Leistungsfach")
      .replace(/(?<![A-Za-zÄÖÜäöüß-])BF(?![A-Za-zÄÖÜäöüß-])/g, "Basisfach");
    return value;
  }

  function shouldSuppressPrintMessage(m, choice) {
    if (m.rule.id === "PROFILE-TRUST") return true;
    if (choice && m.rule.id === "AGVO-COURSES") {
      const lfCourses = choice.selections.filter((s) => s.type === "lf").reduce((a, s) => a + s.semesters.length, 0);
      const otherCourses = choice.selections.filter((s) => s.type !== "lf").reduce((a, s) => a + s.semesters.length, 0);
      if (lfCourses < 12 && otherCourses >= 30) return true;
    }
    return false;
  }

  function formatPrintMessage(m, catalog, choice) {
    switch (m.rule.id) {
      case "AGVO-LF": {
        if (choice) {
          const lfCount = distinct(choice.selections.filter((s) => s.type === "lf").map((s) => s.subjectId)).length;
          if (lfCount < 3) {
            const missing = 3 - lfCount;
            return (missing === 1 ? "Es fehlt noch 1 Leistungsfach." : `Es fehlen noch ${missing} Leistungsfächer.`)
              + " Insgesamt werden drei verschiedene Leistungsfächer über alle vier Halbjahre benötigt, davon mindestens zwei aus Deutsch, Mathematik, fortgeführten Fremdsprachen oder einer Naturwissenschaft (Biologie, Chemie oder Physik).";
          }
          return "Leistungsfächer: Mindestens zwei der drei Leistungsfächer müssen aus Deutsch, Mathematik, fortgeführten Fremdsprachen oder einer Naturwissenschaft (Biologie, Chemie oder Physik) sein.";
        }
        return "Leistungsfächer: Benötigt werden genau drei verschiedene Leistungsfächer über alle vier Halbjahre, davon mindestens zwei aus Deutsch, Mathematik, fortgeführten Fremdsprachen oder einer Naturwissenschaft (Biologie, Chemie oder Physik).";
      }
      case "AGVO-COURSES": {
        if (choice) {
          const otherCourses = choice.selections.filter((s) => s.type !== "lf").reduce((a, s) => a + s.semesters.length, 0);
          if (otherCourses < 30) {
            const missing = 30 - otherCourses;
            return `Kursanzahl: Es ${missing === 1 ? "fehlt noch 1 weiterer Halbjahreskurs" : `fehlen noch ${missing} weitere Halbjahreskurse`} bis zur Mindestzahl von 42 Kursen (12 Leistungsfachkurse und mindestens 30 weitere Kurse).`;
          }
        }
        return "Kursanzahl: Insgesamt werden 12 Leistungsfachkurse und mindestens 30 weitere Halbjahreskurse (zusammen mindestens 42 Kurse) benötigt. Die Schüler-Ingenieur-Akademie (SIA) zählt dabei mit ihren zwei Halbjahren.";
      }
      case "AGVO-REQUIRED": {
        if (m.subjectIds.some((s) => s === "BK" || s === "MU")) {
          return "Bildende Kunst oder Musik: Es fehlen durchgehend vier Halbjahreskurse (HJ 1–4) in einem der beiden Fächer.";
        }
        if (m.subjectIds.some((s) => s === "ETH" || s === "ER" || s === "KR")) {
          return "Religion oder Ethik: In allen vier Halbjahren (HJ 1–4) muss Religion oder Ethik belegt sein.";
        }
        if (m.subjectIds.length === 1) {
          const subject = m.subjectIds[0];
          const sems = m.semesters.length === 0 ? "HJ 1–4" : formatPrintSemesters(m.semesters);
          return `${printSubjectName(catalog, subject)}: Pflichtbelegung fehlt (${sems}).`;
        }
        return sanitizePrintText(m.explanation, catalog);
      }
      case "AGVO-LANGUAGE-SCIENCE":
        return "Sprachen und Naturwissenschaften: Erforderlich sind mindestens eine fortgeführte Fremdsprache und eine Naturwissenschaft (Biologie, Chemie oder Physik) über HJ 1–4 sowie zusätzlich eine zweite fortgeführte Fremdsprache oder eine zweite Naturwissenschaft (bei entsprechender Vorbildung kann Informatik die zweite Naturwissenschaft ersetzen).";
      case "AGVO-SEMESTERS": {
        const subject = m.subjectIds[0];
        const prefix = subject ? printSubjectName(catalog, subject) + ": " : "";
        const extra = subject === "GK" || subject === "GEO"
          ? " Bei Leistungsfach Wirtschaft sind Gemeinschaftskunde in HJ 1/2 und Geographie in HJ 3/4 einzeln wählbar; zusätzliche Kurse in Gemeinschaftskunde in HJ 3 und 4 bleiben zusammen."
          : "";
        return prefix + "Kurse werden in der Regel für ganze Schuljahre (HJ 1 und 2 bzw. HJ 3 und 4) gewählt." + extra;
      }
      case "GSM-GK-CONTINUITY":
        return "Gemeinschaftskunde: Bei Leistungsfach Wirtschaft darf Gemeinschaftskunde nach einer Pause in HJ 2 nicht in HJ 3 und 4 weiterbelegt werden. Für Gemeinschaftskunde in HJ 3 und 4 bitte auch HJ 2 wählen.";
      case "GSM-RELIGION-EXCLUSIVE":
        return "Religion oder Ethik: Ethik und Religion sowie verschiedene Religionsfächer dürfen nicht gleichzeitig belegt werden. Bitte nur ein Fach wählen.";
      case "GSM-RELIGION-CONFESSION":
        return "Religionswahl: Evangelische bzw. Katholische Religion ist nur passend zum in Klasse 10 besuchten Fach wählbar (Ethik bleibt wählbar). Bei Fragen bitte an die Oberstufenberatung wenden.";
      case "PLAN-ORAL": {
        if (m.explanation.includes("Aufgabenfelder")) {
          return "Mündliche Prüfungsfächer: Zusammen mit den drei Leistungsfächern und zwei mündlichen Prüfungsfächern müssen Deutsch, Mathematik und alle drei Aufgabenfelder abgedeckt sein.";
        }
        if (m.explanation.includes("Zwei verschiedene")) {
          return "Mündliche Prüfungsfächer: Es müssen zwei verschiedene Basisfächer als mündliche Prüfungsfächer geplant werden.";
        }
        if (m.explanation.includes("passt nicht mehr")) {
          return "Mündliche Prüfungsfächer: Die bisherige mündliche Prüfungsplanung passt nicht mehr zu den gewählten Kursen und muss neu festgelegt werden.";
        }
        return sanitizePrintText(m.explanation, catalog);
      }
      case "AGVO-EXAM":
        return "Prüfungsfächer: Aus den aktuell belegten Kursen ergibt sich noch keine zulässige Kombination aus drei Leistungsfächern und zwei mündlichen Prüfungsfächern für Deutsch, Mathematik und alle drei Aufgabenfelder.";
      case "PLAN-HOURS": {
        const match = /Durchschnitt\s+([0-9.,]+)\s+Wochenstunden/.exec(m.explanation);
        return match
          ? `Wochenstunden: Durchschnittlich ${match[1]} Wochenstunden pro Halbjahr (Planungsrichtwert sind 32 Wochenstunden; dies ist ein Hinweis, kein automatischer Fehler).`
          : sanitizePrintText(m.explanation, catalog);
      }
      default:
        return sanitizePrintText(m.explanation, catalog);
    }
  }

  function mountBrowserApp(catalogRaw) {
    const doc = root.document;
    if (!doc) return;
    const session = createSession(catalogRaw, { allowSelfServicePrerequisites: true });
    let expandedTask = null;
    let errorText = "";
    let previousErrorText = "";
    let successText = "";
    let lastRenderedStep = session.step;
    let autosaveTimer = null;

    const el = (tag, attrs = {}, children = []) => {
      const node = doc.createElement(tag);
      for (const [k, v] of Object.entries(attrs)) {
        if (k === "className") node.className = v;
        else if (k === "textContent") node.textContent = v;
        else if (k === "checked") node.checked = Boolean(v);
        else if (k === "disabled") node.disabled = Boolean(v);
        else if (k.startsWith("on") && typeof v === "function") node.addEventListener(k.slice(2).toLowerCase(), v);
        else if (v !== null && v !== undefined) node.setAttribute(k, String(v));
      }
      for (const child of (Array.isArray(children) ? children : [children])) {
        if (child === null || child === undefined) continue;
        node.appendChild(typeof child === "string" ? doc.createTextNode(child) : child);
      }
      return node;
    };

    const noticeBox = (text, kind) => el("div", { className: `notice notice-${kind}`, textContent: displayText(text) });
    const optionBox = (child, selected) => el("div", { className: `option-box${selected ? " selected" : ""}` }, child);

    const subtitleEl = doc.getElementById("Subtitle");
    const stateBadgeEl = doc.getElementById("StateBadge");
    const stepperEl = doc.getElementById("Stepper");
    const errorNoticeEl = doc.getElementById("ErrorNotice");
    const errorNoticeTextEl = doc.getElementById("ErrorNoticeText");
    const dismissErrorBtn = doc.getElementById("DismissError");
    const successNoticeEl = doc.getElementById("SuccessNotice");
    const successNoticeTextEl = doc.getElementById("SuccessNoticeText");
    const dismissSuccessBtn = doc.getElementById("DismissSuccess");
    const stepContentEl = doc.getElementById("StepContent");
    const planPanelEl = doc.getElementById("PlanPanel");
    const prevBtn = doc.getElementById("PreviousStep");
    const stepBlockerHintEl = doc.getElementById("StepBlockerHint");
    const nextBtn = doc.getElementById("NextStep");
    const saveBtn = doc.getElementById("SaveButton");
    const loadBtn = doc.getElementById("LoadButton");
    const resetBtn = doc.getElementById("ResetButton");
    const autosaveBox = doc.getElementById("AutosaveCheckbox");
    const hoursWarningEl = doc.getElementById("HoursWarning");
    const fileStatusEl = doc.getElementById("FileStatus");
    const prereqSummaryEl = doc.getElementById("PrerequisitesSummary");
    const prereqListEl = doc.getElementById("PrerequisitesList");
    const modalEl = doc.getElementById("ConfirmModal");
    const modalMsgEl = doc.getElementById("ConfirmMessage");
    const modalCancelBtn = doc.getElementById("ConfirmCancel");
    const modalAcceptBtn = doc.getElementById("ConfirmAccept");
    const printSheetEl = doc.getElementById("PrintSheet");

    let pendingConfirm = null;
    function confirmDialog(text, acceptLabel = "Bestätigen") {
      return new Promise((resolve) => {
        pendingConfirm = resolve;
        modalMsgEl.textContent = text;
        modalAcceptBtn.textContent = acceptLabel;
        modalEl.classList.remove("hidden");
        modalCancelBtn.focus();
      });
    }
    function closeConfirm(result) {
      modalEl.classList.add("hidden");
      const fn = pendingConfirm;
      pendingConfirm = null;
      if (fn) fn(result);
    }
    modalCancelBtn.addEventListener("click", () => closeConfirm(false));
    modalAcceptBtn.addEventListener("click", () => closeConfirm(true));

    function offerLabel(offer) {
      const type = offer.type === "lf" ? "LF" : offer.type === "bf" ? "BF" : offer.type === "elective" ? "Wahlfach" : "Seminarkurs";
      const hours = distinct(offer.schedule.map((h) => h.weeklyHours));
      const sems = formatSemesters(offer.schedule.map((h) => h.semester));
      return `${session.subjectName(offer.subjectId)} · ${type}${offer.variant === "bilingual" ? " · bilingual" : ""} · ${hours.join("/")} h · HJ ${sems}${CourseAnnotations.suffix(offer)}`;
    }

    function progressText() {
      const count = session.countedCourses;
      const missing = MINIMUM_COURSES - count;
      return `${count} von ${MINIMUM_COURSES} Halbjahreskursen` + (missing > 0 ? ` – noch ${missing}` : " – Planungsziel erreicht (nicht geprüft)");
    }

    function scheduleAutosave() {
      if (autosaveTimer) clearTimeout(autosaveTimer);
      if (autosaveBox.checked && session.isDirty) {
        autosaveTimer = setTimeout(() => {
          try {
            session.saveToBrowser(root.localStorage);
          } catch (e) {
            errorText = e.message;
            render();
          }
        }, 1500);
      }
    }

    function edit(action) {
      if (autosaveTimer) clearTimeout(autosaveTimer);
      try {
        errorText = "";
        successText = "";
        action();
        scheduleAutosave();
      } catch (err) {
        errorText = err && err.message ? err.message : String(err);
        render();
      }
    }

    function addRestricted(parent, offers, header) {
      if (offers.length === 0) return;
      const body = el("div", { className: "expander-body" });
      for (const offer of offers) {
        body.appendChild(el("p", {
          className: "small-muted",
          textContent: displayText(`${offerLabel(offer)}: ${session.availability(offer).explanation}`)
        }));
      }
      parent.appendChild(el("details", { className: "expander" }, [
        el("summary", { textContent: `${offers.length} ${header}` }),
        body
      ]));
    }

    function renderPrerequisitesStep(container) {
      container.appendChild(el("h2", { className: "step-title", textContent: "Deine Vorinfos für den Probelauf" }));
      container.appendChild(el("p", { className: "step-hint", textContent: "Gib hier deine bisherigen Fächer und dein Profil aus der Mittelstufe an. In der echten Schulversion sind diese Angaben bereits durch die Oberstufenberatung hinterlegt." }));

      const p = session.profile.prerequisites;
      const hasF = p.continuedLanguages.some((l) => l.toUpperCase() === "F");
      const hasL = p.continuedLanguages.some((l) => l.toUpperCase() === "L");
      const hasI = p.italianProfile === "yes";

      const buildLanguages = (f, l, i) => {
        const list = ["E"];
        if (f) list.push("F");
        if (l) list.push("L");
        if (i) list.push("I");
        return list;
      };
      const updatePrior = (updater) => edit(() => session.setTestPrerequisites(updater(session.profile.prerequisites)));

      const addRadio = (parent, name, group, label, selected, onSelected) => {
        const input = el("input", {
          type: "radio",
          name: group,
          "data-control-name": name,
          checked: selected,
          onChange: () => { if (input.checked) onSelected(); }
        });
        const lbl = el("label", { className: "option-label" }, [input, el("span", { textContent: label })]);
        parent.appendChild(optionBox(lbl, selected));
      };

      const addCheck = (parent, name, label, selected, onChanged) => {
        const input = el("input", {
          type: "checkbox",
          "data-control-name": name,
          checked: selected,
          onChange: () => onChanged(input.checked)
        });
        const lbl = el("label", { className: "option-label" }, [input, el("span", { textContent: label })]);
        parent.appendChild(optionBox(lbl, selected));
      };

      const langGroup = el("div", { className: "prereq-group" }, [
        el("h3", { className: "section-heading", style: "margin-top:0;", textContent: "Fortgeführte Fremdsprachen (spätestens ab Klasse 8)" }),
        el("p", { className: "muted-text", textContent: "Englisch (1. Fremdsprache) ist am GSM für alle fortgeführt." })
      ]);
      addRadio(langGroup, "Prereq_Lang_F", "Prereq_Lang", "Englisch + Französisch", hasF && !hasL,
        () => updatePrior((x) => ({ ...x, continuedLanguages: buildLanguages(true, false, x.italianProfile === "yes") })));
      addRadio(langGroup, "Prereq_Lang_L", "Prereq_Lang", "Englisch + Latein", hasL && !hasF,
        () => updatePrior((x) => ({ ...x, continuedLanguages: buildLanguages(false, true, x.italianProfile === "yes") })));
      addRadio(langGroup, "Prereq_Lang_FL", "Prereq_Lang", "Englisch + Französisch und Latein", hasF && hasL,
        () => updatePrior((x) => ({ ...x, continuedLanguages: buildLanguages(true, true, x.italianProfile === "yes") })));
      container.appendChild(langGroup);

      const profileGroup = el("div", { className: "prereq-group" }, [
        el("h3", { className: "section-heading", style: "margin-top:0;", textContent: "Profil & Zusatzqualifikationen (Klasse 8–10)" })
      ]);
      addCheck(profileGroup, "Prereq_Italian", "Italienisch als 3. Fremdsprache (sprachliches Profil ab Klasse 8)", hasI,
        (yes) => updatePrior((x) => ({
          ...x,
          italianProfile: yes ? "yes" : "no",
          continuedLanguages: buildLanguages(
            x.continuedLanguages.some((l) => l.toUpperCase() === "F"),
            x.continuedLanguages.some((l) => l.toUpperCase() === "L"),
            yes
          )
        })));
      addCheck(profileGroup, "Prereq_Imp", "Profilfach IMP (Informatik, Mathematik, Physik) in Klasse 8–10", p.informaticsImp === "yes",
        (yes) => updatePrior((x) => ({ ...x, informaticsImp: yes ? "yes" : "no" })));
      addCheck(profileGroup, "Prereq_InfBridge", "Informatik-Brückenkurs in Klasse 10 besucht", p.informaticsBridge === "yes",
        (yes) => updatePrior((x) => ({ ...x, informaticsBridge: yes ? "yes" : "no" })));
      addCheck(profileGroup, "Prereq_Bili", "Durchgängig bilingualer Bildungsgang (Englisch)", p.continuousBilingual === "yes",
        (yes) => updatePrior((x) => ({ ...x, continuousBilingual: yes ? "yes" : "no" })));
      container.appendChild(profileGroup);

      const relGroup = el("div", { className: "prereq-group" }, [
        el("h3", { className: "section-heading", style: "margin-top:0;", textContent: "Besuchter Religions- bzw. Ethikunterricht in Klasse 10" })
      ]);
      addRadio(relGroup, "Prereq_Rel_ER", "Prereq_Rel", "Evangelische Religion (ER)", p.religionEthicsGrade10 === "ER",
        () => updatePrior((x) => ({ ...x, religionEthicsGrade10: "ER", religionEthicsPermission: "yes", religionEthicsExamPermission: "unknown" })));
      addRadio(relGroup, "Prereq_Rel_KR", "Prereq_Rel", "Katholische Religion (KR)", p.religionEthicsGrade10 === "KR",
        () => updatePrior((x) => ({ ...x, religionEthicsGrade10: "KR", religionEthicsPermission: "yes", religionEthicsExamPermission: "unknown" })));
      addRadio(relGroup, "Prereq_Rel_ETH", "Prereq_Rel", "Ethik (ETH)", p.religionEthicsGrade10 === "ETH",
        () => updatePrior((x) => ({ ...x, religionEthicsGrade10: "ETH", religionEthicsPermission: "yes", religionEthicsExamPermission: "unknown" })));
      addRadio(relGroup, "Prereq_Rel_Unknown", "Prereq_Rel", "Keiner / Wechsel / Sonderfall prüfen", !["ER", "KR", "ETH"].includes(p.religionEthicsGrade10),
        () => updatePrior((x) => ({ ...x, religionEthicsGrade10: null, religionEthicsPermission: "unknown", religionEthicsExamPermission: "unknown" })));
      container.appendChild(relGroup);

      const sport = Array.isArray(p.sportExemptSemesters) ? p.sportExemptSemesters : [];
      const sportGroup = el("div", { className: "prereq-group" }, [
        el("h3", { className: "section-heading", style: "margin-top:0;", textContent: "Sportunterricht in der Kursstufe" })
      ]);
      addRadio(sportGroup, "Prereq_Sport_None", "Prereq_Sport", "Keine Sportbefreiung (reguläre Teilnahme)", sport.length === 0,
        () => updatePrior((x) => ({ ...x, sportExemptSemesters: [], permanentSportExemption: "no" })));
      addRadio(sportGroup, "Prereq_Sport_12", "Prereq_Sport", "Sportbefreiung in HJ 1+2", arraysEqual(sport, [1, 2]),
        () => updatePrior((x) => ({ ...x, sportExemptSemesters: [1, 2], permanentSportExemption: "yes" })));
      addRadio(sportGroup, "Prereq_Sport_34", "Prereq_Sport", "Sportbefreiung in HJ 3+4", arraysEqual(sport, [3, 4]),
        () => updatePrior((x) => ({ ...x, sportExemptSemesters: [3, 4], permanentSportExemption: "yes" })));
      addRadio(sportGroup, "Prereq_Sport_1234", "Prereq_Sport", "Dauerhafte Sportbefreiung in HJ 1–4", arraysEqual(sport, [1, 2, 3, 4]),
        () => updatePrior((x) => ({ ...x, sportExemptSemesters: [1, 2, 3, 4], permanentSportExemption: "yes" })));
      container.appendChild(sportGroup);
    }

    function renderLfStep(container) {
      container.appendChild(el("h2", { className: "step-title", textContent: "Wähle drei Leistungsfächer" }));
      container.appendChild(el("p", { className: "step-hint", textContent: "Mindestens zwei davon aus Deutsch, Mathematik, einer fortgeführten Fremdsprache oder einer Naturwissenschaft. Leistungsfächer laufen immer über HJ 1–4." }));
      if (session.choice.selections.some((s) => s.type !== "lf")) {
        container.appendChild(el("p", { className: "muted-text", textContent: "Wenn du ein bisheriges Basisfach als Leistungsfach wählst, wird seine Basisfachbelegung automatisch entfernt. Gehe anschließend die weiteren Schritte erneut durch." }));
      }
      if (session.lfChangeMessage.length > 0) container.appendChild(noticeBox(session.lfChangeMessage, "warn"));
      const chosen = session.choice.selections.filter((s) => s.type === "lf");
      container.appendChild(el("p", {
        className: "section-heading",
        textContent: chosen.length < 3 ? `${chosen.length} von 3 gewählt – noch ${3 - chosen.length}` : `${chosen.length} von 3 gewählt · zum Tauschen zuerst ein Leistungsfach abwählen`
      }));
      if (chosen.length >= 3 && session.stepError) container.appendChild(noticeBox(plainText(session.stepError, session.catalog), "warn"));
      const offers = [];
      const seen = new Set();
      for (const o of [...session.lfOffers, ...session.catalog.offers.filter((x) => x.type === "lf" && chosen.some((s) => s.offerId === x.id))]) {
        if (!seen.has(o.id)) { seen.add(o.id); offers.push(o); }
      }
      for (const offer of offers) {
        const isChosen = chosen.some((s) => s.offerId === offer.id);
        const disabled = !(isChosen || chosen.length < 3);
        const box = el("input", {
          type: "checkbox",
          "data-control-name": "Offer_" + offer.id.replaceAll("-", "_"),
          checked: isChosen,
          disabled,
          onChange: () => edit(() => session.setSelection(offer.id, box.checked ? offer.schedule.map((h) => h.semester) : []))
        });
        const lbl = el("label", { className: `option-label${disabled ? " disabled" : ""}` }, [box, el("span", { textContent: offerLabel(offer) })]);
        container.appendChild(optionBox(lbl, isChosen));
      }
      addRestricted(container, session.restrictedOffers(true), "Leistungsfach-Angebote derzeit nicht wählbar");
    }

    function renderOralStep(container) {
      container.appendChild(el("h2", { className: "step-title", textContent: "Plane zwei mündliche Prüfungsfächer" }));
      container.appendChild(el("p", { className: "step-hint", textContent: "Zusammen mit deinen Leistungsfächern müssen Deutsch, Mathematik und alle drei Aufgabenfelder abgedeckt sein. Die Planung ist unverbindlich." }));
      if (session.lfChangeMessage.length > 0) container.appendChild(noticeBox(session.lfChangeMessage, "warn"));
      for (const subject of session.forcedOral) {
        container.appendChild(noticeBox(`✓ ${session.subjectName(subject)} ist gesetzt, weil du es nicht als Leistungsfach hast.`, "done"));
      }
      const planned = session.choice.plannedOralSubjects;
      const free = 2 - planned.length;
      container.appendChild(el("p", {
        className: "section-heading",
        textContent: free > 0 ? (free === 1 ? "Noch 1 Fach wählen" : "Noch 2 Fächer wählen") : "Beide Prüfungsfächer geplant"
      }));
      if (planned.length === 2 && session.stepError) container.appendChild(noticeBox(plainText(session.stepError, session.catalog), "warn"));
      const list = distinct([...planned.filter((s) => !session.forcedOral.includes(s)), ...session.usefulOralCandidates]);
      for (const subject of list) {
        const p = session.profile.prerequisites;
        const assessment = session.profile.status === "verified" && ["ETH", "ER", "KR"].includes(subject)
          && p.religionEthicsGrade10 && p.religionEthicsGrade10 !== subject && p.religionEthicsExamPermission === "unknown";
        const isChecked = planned.includes(subject);
        const box = el("input", {
          type: "checkbox",
          "data-control-name": "Oral_" + subject,
          checked: isChecked,
          onChange: () => edit(() => session.setOral(subject, box.checked))
        });
        const lbl = el("label", { className: "option-label" }, [
          box,
          el("span", { textContent: session.subjectName(subject) + (assessment ? " · nicht in Klasse 10 besucht: Feststellungsprüfung zu Beginn von HJ 1 nötig" : "") })
        ]);
        container.appendChild(optionBox(lbl, isChecked));
      }
      const hidden = session.oralCandidates
        .filter((s) => !session.usefulOralCandidates.includes(s) && !planned.includes(s))
        .map((s) => session.subjectName(s));
      if (hidden.length > 0 && free > 0) {
        container.appendChild(el("details", { className: "expander" }, [
          el("summary", { textContent: `${hidden.length} weitere Fächer passen nicht zu einer vollständigen Kombination` }),
          el("p", {
            className: "small-muted",
            textContent: hidden.join(", ") + ". Mit diesen Fächern wären zusammen mit deinen Leistungsfächern Deutsch, Mathematik oder eines der drei Aufgabenfelder nicht abgedeckt. Wenn du eines davon mündlich möchtest, ändere zuerst das andere Prüfungsfach oder deine Leistungsfächer."
          })
        ]));
      }
    }

    function renderAreaStep(container) {
      container.appendChild(el("h2", { className: "step-title", textContent: "Pflichtbereiche" }));
      container.appendChild(el("p", { className: "step-hint", textContent: "Pflichtfächer sind eingetragen und im Plan mit „Pflicht“ markiert. Hier entscheidest du nur noch, wo es eine Auswahl gibt." }));
      const automatic = session.plan.filter((r) => r.isAutomatic && !r.isOpen).map((r) => `${r.label} (HJ ${formatSemesters(r.semesters)})`);
      if (automatic.length > 0) container.appendChild(noticeBox("✓ Eingetragen: " + automatic.join(", "), "done"));
      if (session.automaticMessage.length > 0) container.appendChild(noticeBox(session.automaticMessage, "warn"));
      for (const m of session.validation.messages.filter((msg) => msg.rule.id === "AGVO-RELIGION")) {
        container.appendChild(noticeBox(m.explanation, "warn"));
      }
      const tasks = session.tasks;
      const openCount = tasks.filter((t) => !t.isSatisfied).length;
      const expanded = expandedTask || (tasks.find((t) => !t.isSatisfied && t.choose > 0) || {}).id || null;

      for (const task of tasks) {
        if (task.id === expanded && task.choose > 0) {
          const card = el("div", { className: "task-card" });
          card.appendChild(el("p", { className: "small-muted", textContent: task.isSatisfied ? "Antwort ändern" : `Offene Frage · noch ${openCount} offen` }));
          card.appendChild(el("h3", { className: "task-card-title", textContent: task.title + (task.choose > 1 ? ` – wähle ${task.choose}` : "") }));
          if (task.fulfilledBySubjects.length > 0) {
            card.appendChild(el("p", { className: "muted-text", style: "color: var(--done);", textContent: "Bereits belegt: " + task.fulfilledBySubjects.map(session.subjectName).join(", ") }));
          }
          card.appendChild(el("p", { className: "muted-text", textContent: displayText(task.hint) }));
          const selected = session.selectedTaskOptions(task);
          const replaces = session.replacesTaskAnswer(task);
          if (selected.length > 1) {
            card.appendChild(el("p", { className: "muted-text", textContent: "Du hast mehrere passende Kurse belegt. Abwählen entfernt den jeweiligen Kurs; andere Kurse bleiben erhalten." }));
          }
          for (const offerId of distinct([...task.optionOfferIds, ...task.chosenOfferIds])) {
            const offer = session.catalog.offers.find((o) => o.id === offerId);
            const chosen = selected.includes(offerId);
            const locked = chosen && session.isRequiredSubject(offer.subjectId);
            const disabled = locked || !(chosen || task.optionOfferIds.includes(offerId));
            const input = el("input", {
              type: replaces ? "radio" : "checkbox",
              name: replaces ? task.id : undefined,
              "data-control-name": "Option_" + offerId.replaceAll("-", "_"),
              checked: chosen,
              disabled,
              onChange: () => {
                if (replaces && !input.checked) return;
                edit(() => session.chooseTaskOption(task.id, offerId, input.checked));
              }
            });
            const lbl = el("label", { className: `option-label${disabled ? " disabled" : ""}` }, [
              input,
              el("span", { textContent: offerLabel(offer) + (locked ? " · durch Prüfungsplanung gesetzt" : "") })
            ]);
            card.appendChild(optionBox(lbl, chosen));
          }
          addRestricted(card, task.restricted.map((r) => session.catalog.offers.find((o) => o.id === r.offerId)), "weitere Angebote brauchen ein geprüftes Profil oder sind nicht freigegeben");
          if (task.isSatisfied && expandedTask === task.id) {
            card.appendChild(el("div", {}, el("button", {
              type: "button",
              className: "btn",
              "data-control-name": "TaskDone",
              textContent: "Fertig",
              onClick: () => { expandedTask = null; render(); }
            })));
          }
          container.appendChild(card);
          continue;
        }
        const answer = [
          ...task.fulfilledBySubjects.map(session.subjectName),
          ...session.selectedTaskOptions(task).map((id) => session.subjectName(session.catalog.offers.find((o) => o.id === id).subjectId))
        ];
        const row = el("div", { className: "task-row" }, [
          el("span", {
            className: `task-row-text ${task.isSatisfied ? "satisfied" : "open"}`,
            textContent: task.isSatisfied ? `✓ ${task.title}: ${answer.join(", ")}` : `○ ${task.title} – offen`
          }),
          task.choose > 0 ? el("button", {
            type: "button",
            className: "btn",
            "data-control-name": "Task_" + task.id.replaceAll("-", "_"),
            textContent: task.isSatisfied ? "Ändern" : "Jetzt wählen",
            onClick: () => { expandedTask = task.id; render(); }
          }) : null
        ]);
        container.appendChild(row);
      }
    }

    function renderExtraStep(container) {
      container.appendChild(el("h2", { className: "step-title", textContent: "Weitere Kurse" }));
      container.appendChild(el("p", { className: "step-hint", textContent: "Ergänze Kurse, bis du das Planungsziel erreichst. Regulär wählst du ganze Schuljahre (HJ 1+2 bzw. HJ 3+4). Einzelhalbjahre werden nur bei einer passenden Ausnahme angeboten." }));
      container.appendChild(el("p", {
        className: "section-heading",
        style: session.countedCourses >= MINIMUM_COURSES ? "color: var(--done);" : undefined,
        textContent: progressText()
      }));

      const adjustable = session.choice.selections
        .filter((s) => s.type !== "lf")
        .map((s) => ({ selection: s, offer: session.catalog.offers.find((o) => o.id === s.offerId) }))
        .filter((x) => x.offer && (session.lockedSemesters(x.selection.offerId).length === 0 || x.offer.schedule.length > session.lockedSemesters(x.selection.offerId).length));

      if (adjustable.length > 0) {
        container.appendChild(el("p", { className: "section-heading", textContent: "Deine Kurse anpassen" }));
        for (const { selection, offer } of adjustable) {
          const locked = session.lockedSemesters(selection.offerId);
          const wrapper = el("div", { style: "display:flex; flex-direction:column; gap:4px;" });
          if (locked.length === 0) {
            const box = el("input", {
              type: "checkbox",
              "data-control-name": "Offer_" + offer.id.replaceAll("-", "_"),
              checked: true,
              onChange: () => edit(() => session.setSelection(offer.id, []))
            });
            wrapper.appendChild(el("label", { className: "option-label" }, [box, el("span", { textContent: offerLabel(offer) })]));
          } else {
            wrapper.appendChild(el("span", { textContent: offerLabel(offer) + " · Pflicht in HJ " + formatSemesters(locked) }));
          }
          const unitsRow = el("div", { className: "semester-units" });
          for (const unit of session.semesterSelectionUnits(offer.id)) {
            const unitChecked = unit.every((h) => selection.semesters.includes(h));
            const unitDisabled = offer.type === "seminar" || unit.some((h) => locked.includes(h))
              || !unit.every((h) => offer.schedule.some((s) => s.semester === h));
            const uBox = el("input", {
              type: "checkbox",
              "data-control-name": "Sem_" + offer.id.replaceAll("-", "_") + "_" + unit.join("_"),
              checked: unitChecked,
              disabled: unitDisabled,
              onChange: () => edit(() => {
                const nextSems = uBox.checked
                  ? distinct([...selection.semesters, ...unit])
                  : selection.semesters.filter((h) => !unit.includes(h));
                session.setSelection(offer.id, nextSems);
              })
            });
            unitsRow.appendChild(el("label", { className: `semester-unit-label${unitDisabled ? " disabled" : ""}` }, [uBox, el("span", { textContent: "HJ " + unit.join("+") })]));
          }
          wrapper.appendChild(unitsRow);
          container.appendChild(optionBox(wrapper, true));
        }
      }

      const extras = session.extraOffers;
      if (extras.length > 0) {
        container.appendChild(el("p", { className: "section-heading", textContent: "Kurs hinzufügen" }));
        for (const offer of extras) {
          const box = el("input", {
            type: "checkbox",
            "data-control-name": "Offer_" + offer.id.replaceAll("-", "_"),
            checked: false,
            onChange: () => edit(() => session.setSelection(offer.id, offer.schedule.map((h) => h.semester)))
          });
          container.appendChild(optionBox(el("label", { className: "option-label" }, [
            box,
            el("span", { textContent: offerLabel(offer) + ` · +${offer.schedule.length} Kurse` })
          ]), false));
        }
      }
      addRestricted(container, session.restrictedOffers(false), "weitere Angebote derzeit nicht wählbar");
    }

    function buildPrintSheet() {
      printSheetEl.innerHTML = "";
      printSheetEl.appendChild(el("div", {
        className: "print-banner",
        textContent: "UNVERBINDLICHE TESTVERSION FÜR ZU HAUSE (SELBSTAUSKUNFT) – NICHT ZUR ABGABE IN DER SCHULE"
      }));
      printSheetEl.appendChild(el("div", { className: "print-header" }, [
        el("div", {}, [
          el("h1", { textContent: "GSM-Kurswahl · Kontrollbogen (Testversion)" }),
          el("p", {
            style: "margin: 0;",
            textContent: `Abitur ${session.catalog.graduationYear} · Stand ${new Date().toLocaleString("de-DE")} · Fachangebot ${session.choice.offerVersion}`
          })
        ]),
        el("img", {
          className: "print-logo",
          src: "./school-logo.jpg",
          alt: "Gymnasium bei St. Michael Schwäbisch Hall"
        })
      ]));

      printSheetEl.appendChild(el("h2", { textContent: "Angegebene Vorinfos (Selbstauskunft)" }));
      const prereqTable = el("table", { className: "print-table" });
      for (const item of session.prerequisites) {
        prereqTable.appendChild(el("tr", {}, [
          el("th", { textContent: item.label, style: "width: 42%;" }),
          el("td", { textContent: item.value })
        ]));
      }
      printSheetEl.appendChild(prereqTable);

      printSheetEl.appendChild(el("h2", { textContent: "Deine Belegung nach Halbjahren" }));
      const courseTable = el("table", { className: "print-table" }, [
        el("tr", {}, [
          el("th", { textContent: "Fach und Hinweise" }),
          el("th", { textContent: "Kursart" }),
          el("th", { textContent: "HJ 1" }),
          el("th", { textContent: "HJ 2" }),
          el("th", { textContent: "HJ 3" }),
          el("th", { textContent: "HJ 4" })
        ])
      ]);
      const typeOrder = { lf: 0, bf: 1, elective: 2, seminar: 3 };
      const sortedSel = session.choice.selections.slice().sort((a, b) => {
        const tDiff = (typeOrder[a.type] ?? 9) - (typeOrder[b.type] ?? 9);
        if (tDiff !== 0) return tDiff;
        return session.subjectName(a.subjectId).localeCompare(session.subjectName(b.subjectId), "de");
      });
      for (const s of sortedSel) {
        const offer = session.catalog.offers.find((o) => o.id === s.offerId);
        const typeStr = formatPrintCourseType(s.type);
        const hrs = (h) => s.semesters.includes(h)
          ? String((offer && offer.schedule.find((x) => x.semester === h) && offer.schedule.find((x) => x.semester === h).weeklyHours) || "?")
          : "-";
        courseTable.appendChild(el("tr", {}, [
          el("td", { textContent: session.subjectName(s.subjectId) + (s.variant === "bilingual" ? " bilingual" : "") + CourseAnnotations.suffix(offer) }),
          el("td", { textContent: typeStr }),
          el("td", { textContent: hrs(1) }),
          el("td", { textContent: hrs(2) }),
          el("td", { textContent: hrs(3) }),
          el("td", { textContent: hrs(4) })
        ]));
      }
      printSheetEl.appendChild(courseTable);

      printSheetEl.appendChild(el("h2", { textContent: "Kurse und Wochenstunden" }));
      const sumTable = el("table", { className: "print-table" }, [
        el("tr", {}, [el("th", { textContent: "Übersicht" }), ...session.summary.map((s) => el("th", { textContent: "HJ " + s.semester }))]),
        el("tr", {}, [el("td", { textContent: "Kurse" }), ...session.summary.map((s) => el("td", { textContent: String(s.courseCount) }))]),
        el("tr", {}, [el("td", { textContent: "Wochenstunden" }), ...session.summary.map((s) => el("td", { textContent: String(s.weeklyHours) }))])
      ]);
      printSheetEl.appendChild(sumTable);

      printSheetEl.appendChild(el("h2", { textContent: "Mündliche Prüfungsplanung" }));
      printSheetEl.appendChild(el("p", {
        textContent: session.choice.plannedOralSubjects.length === 0
          ? "Noch keine mündlichen Prüfungsfächer geplant."
          : "Unverbindlich geplant: " + session.choice.plannedOralSubjects.map(session.subjectName).join(", ")
      }));
      if (session.examOptions.length > 0) {
        const examTable = el("table", { className: "print-table" }, [
          el("tr", {}, [el("th", { textContent: "Erstes Fach" }), el("th", { textContent: "Zweites Fach" }), el("th", { textContent: "Mindestanrechnung" })]),
          ...session.examOptions.map((o) => el("tr", {}, [
            el("td", { textContent: session.subjectName(o.first) }),
            el("td", { textContent: session.subjectName(o.second) }),
            el("td", { textContent: o.minimumCredits + " Kurse" })
          ]))
        ]);
        printSheetEl.appendChild(examTable);
      }

      const hasPlanOralInvalid = session.validation.messages.some((m) => m.rule.id === "PLAN-ORAL" && m.rule.severity === "invalid");
      const filteredMessages = session.validation.messages.filter(
        (m) => !shouldSuppressPrintMessage(m, session.choice) && !(m.rule.id === "AGVO-EXAM" && hasPlanOralInvalid)
      );
      const toCorrect = distinct(filteredMessages.filter((m) => m.rule.severity === "invalid").map((m) => formatPrintMessage(m, session.catalog, session.choice)).filter(Boolean));
      const toClarify = distinct(filteredMessages.filter((m) => m.rule.severity !== "invalid").map((m) => formatPrintMessage(m, session.catalog, session.choice)).filter(Boolean));
      if (toCorrect.length === 0 && toClarify.length === 0) {
        printSheetEl.appendChild(el("h2", { textContent: "Prüfergebnis" }));
        printSheetEl.appendChild(el("p", { textContent: "Deine Wahl ist aus Sicht der automatischen Prüfung vollständig." }));
      } else {
        if (toCorrect.length > 0) {
          printSheetEl.appendChild(el("h2", { textContent: "Noch zu korrigieren" }));
          const ul = el("ul", {});
          for (const item of toCorrect) ul.appendChild(el("li", { textContent: item }));
          printSheetEl.appendChild(ul);
        }
        if (toClarify.length > 0) {
          printSheetEl.appendChild(el("h2", { textContent: "Mit der Oberstufenberatung klären" }));
          const ul = el("ul", {});
          for (const item of toClarify) ul.appendChild(el("li", { textContent: item }));
          printSheetEl.appendChild(ul);
        }
      }
    }

    function targetStepForRule(ruleId) {
      switch (ruleId) {
        case "AGVO-LF":
        case "PLAN-LF":
          return 1;
        case "PLAN-ORAL":
        case "AGVO-ORAL":
        case "AGVO-EXAM":
          return 2;
        case "AGVO-ART":
        case "AGVO-RELIGION":
        case "AGVO-LANGUAGE":
        case "AGVO-SCIENCE":
        case "AGVO-BREADTH":
        case "AGVO-REQUIRED":
          return 3;
        case "AGVO-COURSES":
        case "AGVO-SPORT-REPLACEMENT":
        case "PLAN-MIN-COURSES":
          return 4;
        default:
          return null;
      }
    }

    function renderReviewStep(container) {
      container.appendChild(el("h2", { className: "step-title", textContent: "Überblick" }));
      container.appendChild(el("p", { className: "step-hint", textContent: "Hier siehst du, was noch fehlt und welche Punkte die Oberstufenberatung in der echten Wahl mit dir klärt. In dieser Testversion findet keine Abgabe an die Schule statt." }));

      const messages = session.validation.messages;
      const onlyCountMissing = session.countedCourses < MINIMUM_COURSES && session.choice.selections.filter((x) => x.type === "lf").length === 3;
      const rawTodo = [
        ...messages.filter((m) => m.rule.severity === "invalid" && !(onlyCountMissing && m.rule.id === "AGVO-COURSES"))
          .map((m) => ({ text: plainText(m.explanation, session.catalog), targetStep: targetStepForRule(m.rule.id) })),
        ...session.tasks.filter((t) => !t.isSatisfied).map((t) => ({ text: t.title + " ist noch offen (Schritt 3).", targetStep: 3 })),
        ...(session.countedCourses < MINIMUM_COURSES ? [{ text: `Es fehlen noch ${MINIMUM_COURSES - session.countedCourses} Halbjahreskurse bis zur Mindestzahl von ${MINIMUM_COURSES} (Schritt 4).`, targetStep: 4 }] : [])
      ];
      const seenTodo = new Set();
      const todo = rawTodo.filter((item) => {
        if (seenTodo.has(item.text)) return false;
        seenTodo.add(item.text);
        return true;
      });

      const todoBox = el("div", { className: `review-box ${todo.length === 0 ? "ok" : "danger"}` }, [
        el("p", {
          className: "review-box-title",
          style: `color: ${todo.length === 0 ? "var(--done)" : "var(--danger)"};`,
          textContent: todo.length === 0 ? "✓ Nichts mehr zu tun" : `Das musst du noch tun (${todo.length})`
        }),
        ...todo.map((item, idx) => {
          const textNode = el("p", { style: "margin:0; font-size:13px;", textContent: "• " + displayText(item.text) });
          if (item.targetStep !== null && item.targetStep >= session.firstStep && item.targetStep < session.step) {
            const target = item.targetStep;
            return el("div", { className: "todo-row" }, [
              textNode,
              el("button", {
                type: "button",
                className: "btn btn-sm",
                "data-control-name": `TodoJump_${idx}_${target}`,
                textContent: `→ Schritt ${target}`,
                onClick: () => edit(() => { expandedTask = null; session.goToStep(target); })
              })
            ]);
          }
          return textNode;
        }),
        todo.length === 0 ? el("p", { className: "muted-text", textContent: "Deine Wahl ist aus Sicht der automatischen Prüfung vollständig. Wenn du möchtest, kannst du sie als unverbindlichen Kontrollbogen drucken oder als PDF speichern." }) : null
      ]);
      container.appendChild(todoBox);

      const clarify = distinct(messages.filter((m) => m.rule.severity !== "invalid" && !GENERAL_RULE_IDS.includes(m.rule.id))
        .map((m) => plainText(m.explanation, session.catalog)));
      if (clarify.length > 0) {
        container.appendChild(el("div", { className: "review-box warn" }, [
          el("p", { className: "review-box-title", style: "color: var(--warn);", textContent: `Das klärt die Oberstufenberatung mit dir (${clarify.length})` }),
          ...clarify.map((item) => el("p", { style: "margin:0; font-size:13px;", textContent: "• " + displayText(item) })),
          el("p", { className: "small-muted", textContent: "Ein solcher Hinweis bedeutet nicht, dass deine Wahl falsch ist." })
        ]));
      }

      container.appendChild(el("div", { style: "display:flex; flex-wrap:wrap; gap:10px;" }, [
        el("button", {
          type: "button",
          className: "btn",
          id: "ControlSheetButton",
          textContent: "Kontrollbogen drucken / als PDF speichern",
          onClick: () => {
            buildPrintSheet();
            if (typeof root.print === "function") root.print();
          }
        })
      ]));

      container.appendChild(el("p", {
        className: "section-heading",
        style: session.countedCourses >= MINIMUM_COURSES ? "color: var(--done);" : undefined,
        textContent: progressText()
      }));
      const sumTable = el("table", { className: "summary-table" });
      for (const s of session.summary) {
        sumTable.appendChild(el("tr", {}, [
          el("td", { textContent: `HJ ${s.semester}` }),
          el("td", { textContent: `${s.courseCount} Kurse` }),
          el("td", { textContent: `${s.weeklyHours} Wochenstunden` })
        ]));
      }
      container.appendChild(sumTable);

      const examsBody = el("div", { className: "expander-body" });
      for (const exam of session.examOptions) {
        examsBody.appendChild(el("p", { style: "margin:0; font-size:13px;", textContent: `${session.subjectName(exam.first)} + ${session.subjectName(exam.second)} · vorläufige Mindestanrechnung ${exam.minimumCredits}` }));
      }
      if (session.examOptions.length === 0) {
        examsBody.appendChild(el("p", { style: "margin:0; font-size:13px;", textContent: "Noch keine passende Kombination aus den belegten Kursen. Pflichtbelegung und offene Punkte prüfen." }));
      }
      examsBody.appendChild(el("p", { className: "small-muted", textContent: "Die mündlichen Prüfungsfächer legst du erst in der Kursstufe verbindlich fest. Die Oberstufenberatung bestätigt die persönlichen Prüfungsvoraussetzungen und die endgültige Anrechnung." }));
      container.appendChild(el("details", { className: "expander expander-lg" }, [
        el("summary", { textContent: `Mögliche mündliche Kombinationen aus deiner Belegung (${session.examOptions.length})` }),
        examsBody
      ]));

      const detailsBody = el("div", { className: "expander-body" });
      for (const note of session.planningNotes) {
        detailsBody.appendChild(el("p", { className: "small-muted", textContent: "• " + displayText(note) }));
      }
      for (const m of messages) {
        detailsBody.appendChild(el("p", {
          className: "small-muted",
          style: m.rule.severity === "invalid" ? "color: var(--danger);" : undefined,
          textContent: displayText(`${m.rule.id}: ${m.explanation}\n${m.remedy}`)
        }));
      }
      container.appendChild(el("details", { className: "expander expander-lg" }, [
        el("summary", { textContent: `Alle Prüfhinweise mit Regelkennung (${messages.length}) – für die Oberstufenberatung` }),
        detailsBody
      ]));
    }

    function renderPlan() {
      planPanelEl.innerHTML = "";
      planPanelEl.appendChild(el("h2", { className: "plan-title", textContent: "Dein Plan" }));
      const rows = session.plan;
      if (rows.length === 0) {
        planPanelEl.appendChild(el("p", { className: "muted-text", textContent: "Noch nichts gewählt. Beginne nach deinen Vorinfos mit deinen Leistungsfächern." }));
      } else {
        const grid = el("div", { className: "plan-grid" });
        grid.appendChild(el("div", { className: "plan-header-cell", textContent: "" }));
        for (let h = 1; h <= 4; h++) grid.appendChild(el("div", { className: "plan-header-cell", textContent: "HJ " + h }));

        rows.forEach((row, idx) => {
          const badges = [el("span", { className: row.isOpen ? "plan-row-open" : "", textContent: row.label })];
          if (row.type === "lf") badges.push(el("span", { className: "badge badge-lf", textContent: "LF" }));
          if (row.variant === "bilingual") badges.push(el("span", { className: "badge badge-empty", textContent: "bili" }));
          if (row.isAutomatic && !row.isOpen) badges.push(el("span", { className: "badge badge-empty", textContent: "Pflicht" }));
          if (row.isOral) badges.push(el("span", { className: "badge badge-oral", textContent: "mündl." }));
          if (row.issue === "invalid") badges.push(el("span", { className: "badge badge-invalid", textContent: "Fehler", title: displayText(row.issueText || "") }));
          else if (row.issue === "needs_review" && !row.isOpen) badges.push(el("span", { className: "badge badge-review", textContent: "klären", title: displayText(row.issueText || "") }));

          if (row.step < session.step) {
            const btn = el("button", {
              type: "button",
              className: "plan-row-btn",
              "data-control-name": "Plan_" + idx,
              title: displayText(row.issueText ? `${row.issueText}\n\nKlicken, um zu Schritt ${row.step} zu springen.` : `Zu Schritt ${row.step} springen`),
              onClick: () => edit(() => { expandedTask = null; session.goToStep(row.step); })
            }, badges);
            grid.appendChild(btn);
          } else {
            grid.appendChild(el("div", {
              className: "plan-row-label",
              title: displayText(row.issueText || "")
            }, badges));
          }

          for (let h = 1; h <= 4; h++) {
            const active = row.semesters.includes(h);
            let cls = "plan-cell";
            if (row.isOpen) {
              cls += active ? " open" : " hidden";
            } else if (active) {
              cls += row.type === "lf" ? " lf" : row.type === "bf" ? " bf" : " other";
            }
            grid.appendChild(el("div", { className: cls }));
          }
        });
        planPanelEl.appendChild(grid);
        if (session.step > session.firstStep) {
          planPanelEl.appendChild(el("p", { className: "tiny-muted", textContent: "Tipp: Klicke ein Fach an, um zum zugehörigen Schritt zurückzuspringen." }));
        }
      }

      const legend = el("div", { className: "legend" }, [
        el("span", { className: "legend-item" }, [el("span", { className: "legend-swatch", style: "background: var(--lf-fill);" }), "Leistungsfach"]),
        el("span", { className: "legend-item" }, [el("span", { className: "legend-swatch", style: "background: var(--bf-fill);" }), "Basisfach"]),
        el("span", { className: "legend-item" }, [el("span", { className: "legend-swatch", style: "background: var(--other-fill);" }), "Wahlfach/Seminar"]),
        el("span", { textContent: "gestrichelt = noch offen" })
      ]);
      planPanelEl.appendChild(legend);
      planPanelEl.appendChild(el("p", { style: "margin:0; font-size:14px; font-weight:600;", textContent: progressText() }));
      const pct = Math.min(100, Math.round((Math.min(session.countedCourses, MINIMUM_COURSES) / MINIMUM_COURSES) * 100));
      planPanelEl.appendChild(el("div", { className: "progress-track" }, el("div", { className: "progress-fill", style: `width: ${pct}%;` })));
      planPanelEl.appendChild(el("div", { className: "semester-totals" },
        session.summary.map((s) => el("span", { textContent: `HJ ${s.semester}: ${s.courseCount} Kurse · ${s.weeklyHours} Wochenstunden` }))));
      planPanelEl.appendChild(el("p", { className: "tiny-muted", textContent: "Belegungszahlen und Wochenstunden sind keine Anrechnungs- oder Zulassungsbestätigung." }));
    }

    function renderStepper() {
      stepperEl.innerHTML = "";
      const open = session.tasks.filter((t) => !t.isSatisfied).length;
      for (let i = session.firstStep; i <= session.lastStep; i++) {
        const label = i === 0 ? "Vorinfos"
          : STEP_NAMES[i - 1] + (i === 3 && session.step >= 3 && open > 0 ? ` · ${open} offen` : "");
        const current = i === session.step;
        const done = i < session.step;
        const text = (done ? "✓ " : `${i} `) + label;
        if (!done) {
          stepperEl.appendChild(el("span", {
            className: `step-pill${current ? " current" : ""}`,
            "data-control-name": "Step_" + i,
            textContent: text
          }));
        } else {
          stepperEl.appendChild(el("button", {
            type: "button",
            className: "step-btn",
            "data-control-name": "Step_" + i,
            title: `Zurück zu Schritt ${i}`,
            textContent: text,
            onClick: () => edit(() => { expandedTask = null; session.goToStep(i); })
          }));
        }
      }
    }

    function render() {
      const activeControlName = doc.activeElement && doc.activeElement.getAttribute("data-control-name");
      subtitleEl.textContent = `Unverbindliche Testversion für zu Hause (Selbstauskunft) · Abitur ${session.catalog.graduationYear} · keine Abgabe an die Schule`;

      const stepChanged = lastRenderedStep !== session.step;
      lastRenderedStep = session.step;

      renderStepper();
      stepContentEl.innerHTML = "";
      if (session.step === 0) renderPrerequisitesStep(stepContentEl);
      else if (session.step === 1) renderLfStep(stepContentEl);
      else if (session.step === 2) renderOralStep(stepContentEl);
      else if (session.step === 3) renderAreaStep(stepContentEl);
      else if (session.step === 4) renderExtraStep(stepContentEl);
      else renderReviewStep(stepContentEl);

      renderPlan();

      if (stepChanged && typeof stepContentEl.scrollIntoView === "function" && (root.scrollY || 0) > 120) {
        stepContentEl.scrollIntoView({ behavior: "auto", block: "start" });
      }

      const hoursMsg = session.validation.messages.find((m) => m.rule.id === "GSM-HOURS-UPPER");
      if (hoursMsg) {
        hoursWarningEl.textContent = hoursMsg.explanation;
        hoursWarningEl.classList.remove("hidden");
      } else {
        hoursWarningEl.classList.add("hidden");
      }

      const errorChanged = Boolean(errorText) && errorText !== previousErrorText;
      previousErrorText = errorText;
      if (errorText) {
        if (errorNoticeTextEl) errorNoticeTextEl.textContent = errorText;
        else errorNoticeEl.textContent = errorText;
        errorNoticeEl.classList.remove("hidden");
        if (errorChanged && typeof errorNoticeEl.scrollIntoView === "function") {
          errorNoticeEl.scrollIntoView({ behavior: "smooth", block: "nearest" });
        }
      } else {
        if (errorNoticeTextEl) errorNoticeTextEl.textContent = "";
        else errorNoticeEl.textContent = "";
        errorNoticeEl.classList.add("hidden");
      }

      if (successNoticeEl) {
        if (successText) {
          if (successNoticeTextEl) successNoticeTextEl.textContent = successText;
          else successNoticeEl.textContent = successText;
          successNoticeEl.classList.remove("hidden");
        } else {
          if (successNoticeTextEl) successNoticeTextEl.textContent = "";
          successNoticeEl.classList.add("hidden");
        }
      }

      prevBtn.disabled = session.step <= session.firstStep;
      nextBtn.style.display = session.step < session.lastStep ? "" : "none";
      nextBtn.disabled = Boolean(session.stepError);
      nextBtn.textContent = session.step === 3 && session.tasks.some((t) => !t.isSatisfied) ? "Später entscheiden, weiter" : "Weiter";
      if (stepBlockerHintEl) {
        const blocker = session.step < session.lastStep && session.stepError ? displayText(plainText(session.stepError, session.catalog)) : "";
        stepBlockerHintEl.textContent = blocker;
        stepBlockerHintEl.title = blocker;
        stepBlockerHintEl.classList.toggle("hidden", blocker.length === 0);
      }

      const saved = !session.isDirty && session.hasSavedFile;
      stateBadgeEl.textContent = session.isDirty
        ? "Noch nicht gespeichert"
        : saved ? "Im Browser gespeichert · unverbindlicher Probelauf" : "Vorläufig · Testversion für zu Hause";
      stateBadgeEl.className = `pill ${saved ? "pill-done" : "pill-warn"}`;

      const hideStatus = session.message.startsWith("Ungespeicherte Änderungen") || session.message.startsWith("Entwurf im Browser gespeichert") || session.message.startsWith("Neuer vorläufiger");
      fileStatusEl.textContent = session.message;
      fileStatusEl.classList.toggle("hidden", hideStatus);

      prereqSummaryEl.textContent = "Deine Voraussetzungen und technische Details";
      prereqListEl.innerHTML = "";
      prereqListEl.appendChild(el("p", { className: "tiny-muted", textContent: session.profileStatusText }));
      for (const item of session.prerequisites) {
        prereqListEl.appendChild(el("p", { className: "tiny-muted", textContent: `${item.label}: ${item.value}.` }));
      }
      prereqListEl.appendChild(el("p", { className: "tiny-muted", textContent: "Speicherung: Lokal im Browser (localStorage) · Keine Übertragung ins Schulnetz." }));

      if (activeControlName) {
        const target = doc.querySelector(`[data-control-name="${activeControlName}"]`);
        if (target && typeof target.focus === "function") target.focus();
      }
    }

    if (dismissErrorBtn) dismissErrorBtn.addEventListener("click", () => { errorText = ""; render(); });
    if (dismissSuccessBtn) dismissSuccessBtn.addEventListener("click", () => { successText = ""; render(); });
    prevBtn.addEventListener("click", () => edit(() => session.previousStep()));
    nextBtn.addEventListener("click", () => edit(() => { expandedTask = null; session.nextStep(); }));
    saveBtn.addEventListener("click", () => {
      edit(() => session.saveToBrowser(root.localStorage));
      if (!errorText && session.hasSavedFile) {
        successText = `✓ Entwurf im Browser gespeichert (Revision ${session.choice.revision}).`;
        render();
      }
    });
    loadBtn.addEventListener("click", async () => {
      if (autosaveTimer) clearTimeout(autosaveTimer);
      if (!session.isDirty || await confirmDialog("Ungespeicherte Änderungen verwerfen und den im Browser gespeicherten Entwurf laden?", "Änderungen verwerfen")) {
        edit(() => session.loadFromBrowser(root.localStorage));
      }
    });
    resetBtn.addEventListener("click", async () => {
      if (autosaveTimer) clearTimeout(autosaveTimer);
      if (await confirmDialog("Aktuellen Probelauf und alle gespeicherten Testdaten im Browser zurücksetzen?", "Neu beginnen")) {
        expandedTask = null;
        edit(() => session.resetDraft(root.localStorage));
      }
    });

    session.onChanged(render);
    try {
      if (root.localStorage && root.localStorage.getItem(STORAGE_KEY)) {
        session.loadFromBrowser(root.localStorage);
      }
    } catch (_) {
      // Ignore corrupt localStorage entries and start fresh.
    }
    render();
  }

  const exported = {
    RULE_VERSION,
    MINIMUM_COURSES,
    CourseAnnotations,
    CatalogRules,
    AcademicRules,
    ChoiceValidator,
    ChoiceWorkflow,
    createSession,
    createMissingProfile,
    createDefaultTestPrerequisites,
    createDefaultTestProfile,
    normalizeCatalog,
    normalizePriorEducation,
    displayText,
    plainText,
    formatSemesters
  };

  if (typeof module !== "undefined" && module.exports) {
    module.exports = exported;
  } else {
    root.GsmKurswahl = exported;
    if (root.document) {
      root.document.addEventListener("DOMContentLoaded", async () => {
        let catalog = root.GSM_DEFAULT_CATALOG || null;
        if (typeof root.fetch === "function" && root.location && root.location.protocol !== "file:") {
          try {
            const resp = await root.fetch("./fachangebot.json", { cache: "no-store" });
            if (resp.ok) catalog = await resp.json();
          } catch (_) {
            // Fall back to embedded catalog.js
          }
        }
        if (catalog) mountBrowserApp(catalog);
      });
    }
  }
})(typeof globalThis !== "undefined" ? globalThis : this);
