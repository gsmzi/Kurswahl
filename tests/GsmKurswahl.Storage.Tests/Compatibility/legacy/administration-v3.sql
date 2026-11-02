-- Lehrerbestand Schema 3 (Stand 28.10.2026), synthetisch. Historisches Format: niemals ändern.
BEGIN;
CREATE TABLE students (id TEXT NOT NULL, version INTEGER NOT NULL, body TEXT NOT NULL, PRIMARY KEY(id,version));
CREATE TABLE revisions (seq INTEGER PRIMARY KEY, id TEXT NOT NULL UNIQUE, student TEXT NOT NULL, body TEXT NOT NULL);
CREATE TABLE reselections (seq INTEGER PRIMARY KEY, student TEXT NOT NULL, body TEXT NOT NULL);
CREATE TABLE profiles (student TEXT NOT NULL, revision INTEGER NOT NULL, body TEXT NOT NULL, PRIMARY KEY(student,revision));
CREATE TABLE distributions (seq INTEGER PRIMARY KEY, body TEXT NOT NULL);
CREATE TABLE planning_snapshots (seq INTEGER PRIMARY KEY, id TEXT NOT NULL UNIQUE, body TEXT NOT NULL);
CREATE TABLE planning_decisions (seq INTEGER PRIMARY KEY, body TEXT NOT NULL);
CREATE TABLE transfer_receipts (seq INTEGER PRIMARY KEY, body TEXT NOT NULL);
INSERT INTO students VALUES('demo-001',1,'{
  "schemaVersion": 1,
  "student": {
    "id": "22222222-2222-2222-2222-222222222222",
    "externalStudentId": "demo-001",
    "syntheticDisplayName": "Demo Eins",
    "className": "10a",
    "graduationYear": 2029,
    "departure": "expected_departure",
    "prerequisites": {
      "italianProfile": "yes",
      "informaticsBridge": "no",
      "informaticsImp": "unknown",
      "continuousBilingual": "yes",
      "religionEthicsGrade10": "KR",
      "sportExemptSemesters": [
        1
      ],
      "continuedLanguages": [
        "E",
        "F"
      ],
      "religionEthicsPermission": "yes",
      "religionEthicsExamPermission": "no",
      "permanentSportExemption": "no"
    }
  },
  "windowsUsername": "DEMO\\eins",
  "accountId": "",
  "homeDirectory": "10a/Eins.Demo",
  "mappingConfirmed": false,
  "version": 1,
  "actor": "demo-beratung",
  "changedAt": "2026-11-02T08:00:00+00:00",
  "origin": {
    "sourceId": "klasse10.csv",
    "contentHash": "ABC",
    "mappingVersion": "map-1",
    "importedAt": "2026-11-02T08:00:00+00:00",
    "authenticatedAccount": "DEMO\\beratung"
  }
}');
INSERT INTO students VALUES('demo-001',2,'{
  "schemaVersion": 1,
  "student": {
    "id": "22222222-2222-2222-2222-222222222222",
    "externalStudentId": "demo-001",
    "syntheticDisplayName": "Demo Eins",
    "className": "10a",
    "graduationYear": 2029,
    "departure": "expected_departure",
    "prerequisites": {
      "italianProfile": "yes",
      "informaticsBridge": "no",
      "informaticsImp": "unknown",
      "continuousBilingual": "yes",
      "religionEthicsGrade10": "KR",
      "sportExemptSemesters": [
        1
      ],
      "continuedLanguages": [
        "E",
        "F"
      ],
      "religionEthicsPermission": "yes",
      "religionEthicsExamPermission": "no",
      "permanentSportExemption": "no"
    }
  },
  "windowsUsername": "DEMO\\eins",
  "accountId": "",
  "homeDirectory": "C:\\Demo\\10a\\Eins.Demo",
  "mappingConfirmed": true,
  "version": 2,
  "actor": "demo-beratung",
  "changedAt": "2026-11-02T08:00:00+00:00",
  "origin": {
    "sourceId": "klasse10.csv",
    "contentHash": "ABC",
    "mappingVersion": "map-1",
    "importedAt": "2026-11-02T08:00:00+00:00",
    "authenticatedAccount": "DEMO\\beratung"
  }
}');
INSERT INTO revisions(id,student,body) VALUES('33333333-3333-3333-3333-333333333333','demo-001','{
  "id": "33333333-3333-3333-3333-333333333333",
  "previousRevisionId": null,
  "studentId": "demo-001",
  "changedAt": "2026-11-02T08:00:00+00:00",
  "actorId": "demo-beratung",
  "actorRole": "student",
  "source": "student_submission",
  "reason": "Automatische Sammlung aus bestätigtem Schülerordner",
  "offerVersion": "demo-2029-v3",
  "ruleVersion": "m2-2",
  "snapshot": {
    "schemaVersion": 2,
    "documentId": "11111111-1111-1111-1111-111111111111",
    "studentId": "demo-001",
    "graduationYear": 2029,
    "offerVersion": "demo-2029-v3",
    "ruleVersion": "m2-2",
    "revision": 3,
    "createdAt": "2026-11-02T08:00:00+00:00",
    "updatedAt": "2026-11-02T09:00:00+00:00",
    "submissionStatus": "draft",
    "phase": "preliminary",
    "selections": [
      {
        "offerId": "D-LF",
        "subjectId": "D",
        "type": "lf",
        "variant": "normal",
        "semesters": [
          1,
          2,
          3,
          4
        ]
      }
    ],
    "plannedOralSubjects": [
      "D",
      "NEU"
    ]
  },
  "changes": [
    {
      "offerId": "D-LF",
      "before": [],
      "after": [
        1,
        2,
        3,
        4
      ]
    }
  ],
  "importOrigin": {
    "sourceId": "aktuell.json",
    "contentHash": "ABC",
    "mappingVersion": "m3-choice-v1",
    "importedAt": "2026-11-02T08:00:00+00:00",
    "authenticatedAccount": "DEMO\\eins"
  }
}');
INSERT INTO reselections(student,body) VALUES('demo-001','{
  "required": true,
  "deadline": "2026-11-09T08:00:00+00:00",
  "note": "demo-beratung: Nachwahl",
  "at": "2026-10-28T12:00:00+00:00"
}');
INSERT INTO profiles VALUES('demo-001',1,'{
  "profile": {
    "schemaVersion": 1,
    "profileRevision": 1,
    "studentId": "demo-001",
    "windowsUsername": "",
    "graduationYear": 2029,
    "issuedAt": "2026-10-27T10:00:00+00:00",
    "keyId": "local-profile",
    "prerequisites": {
      "italianProfile": "yes",
      "informaticsBridge": "no",
      "informaticsImp": "yes",
      "continuousBilingual": "yes",
      "religionEthicsGrade10": "KR",
      "sportExemptSemesters": [
        1
      ],
      "continuedLanguages": [
        "E",
        "F"
      ],
      "religionEthicsPermission": "yes",
      "religionEthicsExamPermission": "no",
      "permanentSportExemption": "no"
    },
    "accountId": ""
  },
  "profileBytes": "ewogICJzY2hlbWFWZXJzaW9uIjogMSwKICAicHJvZmlsZVJldmlzaW9uIjogMSwKICAic3R1ZGVudElkIjogImRlbW8tMDAxIiwKICAid2luZG93c1VzZXJuYW1lIjogIiIsCiAgImdyYWR1YXRpb25ZZWFyIjogMjAyOSwKICAiaXNzdWVkQXQiOiAiMjAyNi0xMC0yN1QxMDowMDowMCswMDowMCIsCiAgImtleUlkIjogImxvY2FsLXByb2ZpbGUiLAogICJwcmVyZXF1aXNpdGVzIjogewogICAgIml0YWxpYW5Qcm9maWxlIjogInllcyIsCiAgICAiaW5mb3JtYXRpY3NCcmlkZ2UiOiAibm8iLAogICAgImluZm9ybWF0aWNzSW1wIjogInllcyIsCiAgICAiY29udGludW91c0JpbGluZ3VhbCI6ICJ5ZXMiLAogICAgInJlbGlnaW9uRXRoaWNzR3JhZGUxMCI6ICJLUiIsCiAgICAic3BvcnRFeGVtcHRTZW1lc3RlcnMiOiBbCiAgICAgIDEKICAgIF0sCiAgICAiY29udGludWVkTGFuZ3VhZ2VzIjogWwogICAgICAiRSIsCiAgICAgICJGIgogICAgXSwKICAgICJyZWxpZ2lvbkV0aGljc1Blcm1pc3Npb24iOiAieWVzIiwKICAgICJyZWxpZ2lvbkV0aGljc0V4YW1QZXJtaXNzaW9uIjogIm5vIiwKICAgICJwZXJtYW5lbnRTcG9ydEV4ZW1wdGlvbiI6ICJubyIKICB9LAogICJhY2NvdW50SWQiOiAiIgp9",
  "signatureBytes": ""
}');
INSERT INTO distributions(body) VALUES('{
  "id": "11111111-1111-1111-1111-111111111111",
  "studentId": "demo-001",
  "profileRevision": 1,
  "at": "2026-11-02T08:00:00+00:00",
  "status": "verteilt",
  "detail": "Einzelprofil verteilt"
}');
INSERT INTO planning_snapshots(id,body) VALUES('11111111-1111-1111-1111-111111111111','{
  "schemaVersion": 1,
  "id": "11111111-1111-1111-1111-111111111111",
  "createdAt": "2026-11-02T08:00:00+00:00",
  "name": "Ausgangsstand",
  "actor": "demo-beratung",
  "ruleVersion": "m2-2",
  "catalog": {
    "schemaVersion": 1,
    "version": "demo-2029-v3",
    "graduationYear": 2029,
    "isDemo": true,
    "subjects": [
      {
        "id": "D",
        "name": "Deutsch"
      },
      {
        "id": "NEU",
        "name": "Neues Fach"
      }
    ],
    "offers": [
      {
        "id": "D-LF",
        "subjectId": "D",
        "type": "lf",
        "variant": "normal",
        "status": "offered",
        "schedule": [
          {
            "semester": 1,
            "weeklyHours": 3
          },
          {
            "semester": 2,
            "weeklyHours": 3
          },
          {
            "semester": 3,
            "weeklyHours": 3
          },
          {
            "semester": 4,
            "weeklyHours": 3
          }
        ],
        "requirements": []
      },
      {
        "id": "NEU-BF",
        "subjectId": "NEU",
        "type": "bf",
        "variant": "bilingual",
        "status": "planned",
        "schedule": [
          {
            "semester": 1,
            "weeklyHours": 3
          },
          {
            "semester": 2,
            "weeklyHours": 3
          },
          {
            "semester": 3,
            "weeklyHours": 3
          },
          {
            "semester": 4,
            "weeklyHours": 3
          }
        ],
        "requirements": [
          "continuous_bilingual"
        ]
      }
    ]
  },
  "students": [
    {
      "schemaVersion": 1,
      "student": {
        "id": "22222222-2222-2222-2222-222222222222",
        "externalStudentId": "demo-001",
        "syntheticDisplayName": "Demo Eins",
        "className": "10a",
        "graduationYear": 2029,
        "departure": "expected_departure",
        "prerequisites": {
          "italianProfile": "yes",
          "informaticsBridge": "no",
          "informaticsImp": "unknown",
          "continuousBilingual": "yes",
          "religionEthicsGrade10": "KR",
          "sportExemptSemesters": [
            1
          ],
          "continuedLanguages": [
            "E",
            "F"
          ],
          "religionEthicsPermission": "yes",
          "religionEthicsExamPermission": "no",
          "permanentSportExemption": "no"
        }
      },
      "windowsUsername": "DEMO\\eins",
      "accountId": "",
      "homeDirectory": "C:\\Demo\\10a\\Eins.Demo",
      "mappingConfirmed": true,
      "version": 2,
      "actor": "demo-beratung",
      "changedAt": "2026-11-02T08:00:00+00:00",
      "origin": {
        "sourceId": "klasse10.csv",
        "contentHash": "ABC",
        "mappingVersion": "map-1",
        "importedAt": "2026-11-02T08:00:00+00:00",
        "authenticatedAccount": "DEMO\\beratung"
      }
    }
  ],
  "choices": [
    {
      "id": "33333333-3333-3333-3333-333333333333",
      "previousRevisionId": null,
      "studentId": "demo-001",
      "changedAt": "2026-11-02T08:00:00+00:00",
      "actorId": "demo-beratung",
      "actorRole": "student",
      "source": "student_submission",
      "reason": "Automatische Sammlung aus bestätigtem Schülerordner",
      "offerVersion": "demo-2029-v3",
      "ruleVersion": "m2-2",
      "snapshot": {
        "schemaVersion": 2,
        "documentId": "11111111-1111-1111-1111-111111111111",
        "studentId": "demo-001",
        "graduationYear": 2029,
        "offerVersion": "demo-2029-v3",
        "ruleVersion": "m2-2",
        "revision": 3,
        "createdAt": "2026-11-02T08:00:00+00:00",
        "updatedAt": "2026-11-02T09:00:00+00:00",
        "submissionStatus": "draft",
        "phase": "preliminary",
        "selections": [
          {
            "offerId": "D-LF",
            "subjectId": "D",
            "type": "lf",
            "variant": "normal",
            "semesters": [
              1,
              2,
              3,
              4
            ]
          }
        ],
        "plannedOralSubjects": [
          "D",
          "NEU"
        ]
      },
      "changes": [
        {
          "offerId": "D-LF",
          "before": [],
          "after": [
            1,
            2,
            3,
            4
          ]
        }
      ],
      "importOrigin": {
        "sourceId": "aktuell.json",
        "contentHash": "ABC",
        "mappingVersion": "m3-choice-v1",
        "importedAt": "2026-11-02T08:00:00+00:00",
        "authenticatedAccount": "DEMO\\eins"
      }
    }
  ],
  "followUps": [
    {
      "studentId": "demo-001",
      "required": true,
      "deadline": "2026-11-09T08:00:00+00:00",
      "note": "Nachwahl"
    }
  ],
  "includeExpectedDepartures": true,
  "targetGroupSize": 20,
  "isScenario": false,
  "basisId": null
}');
INSERT INTO planning_decisions(body) VALUES('{
  "schemaVersion": 1,
  "id": "44444444-4444-4444-4444-444444444444",
  "at": "2026-10-28T12:00:00+00:00",
  "actor": "demo-beratung",
  "reason": "Streichung",
  "basisId": "11111111-1111-1111-1111-111111111111",
  "catalog": {
    "schemaVersion": 1,
    "version": "demo-2029-plan",
    "graduationYear": 2029,
    "isDemo": true,
    "subjects": [
      {
        "id": "D",
        "name": "Deutsch"
      },
      {
        "id": "NEU",
        "name": "Neues Fach"
      }
    ],
    "offers": [
      {
        "id": "D-LF",
        "subjectId": "D",
        "type": "lf",
        "variant": "normal",
        "status": "offered",
        "schedule": [
          {
            "semester": 1,
            "weeklyHours": 3
          },
          {
            "semester": 2,
            "weeklyHours": 3
          },
          {
            "semester": 3,
            "weeklyHours": 3
          },
          {
            "semester": 4,
            "weeklyHours": 3
          }
        ],
        "requirements": []
      },
      {
        "id": "NEU-BF",
        "subjectId": "NEU",
        "type": "bf",
        "variant": "bilingual",
        "status": "planned",
        "schedule": [
          {
            "semester": 1,
            "weeklyHours": 3
          },
          {
            "semester": 2,
            "weeklyHours": 3
          },
          {
            "semester": 3,
            "weeklyHours": 3
          },
          {
            "semester": 4,
            "weeklyHours": 3
          }
        ],
        "requirements": [
          "continuous_bilingual"
        ]
      }
    ]
  }
}');
INSERT INTO transfer_receipts(body) VALUES('{
  "schemaVersion": 1,
  "id": "11111111-1111-1111-1111-111111111111",
  "studentId": "demo-001",
  "choiceRevisionId": "33333333-3333-3333-3333-333333333333",
  "studentVersion": 2,
  "catalogVersion": "demo-2029-v3",
  "ruleVersion": "m2-2",
  "at": "2026-11-02T08:00:00+00:00",
  "actor": "demo-beratung",
  "entered": true,
  "note": "eingegeben"
}');
CREATE TRIGGER students_no_update BEFORE UPDATE ON students BEGIN SELECT RAISE(ABORT,'append-only'); END;
CREATE TRIGGER students_no_delete BEFORE DELETE ON students BEGIN SELECT RAISE(ABORT,'append-only'); END;
CREATE TRIGGER revisions_no_update BEFORE UPDATE ON revisions BEGIN SELECT RAISE(ABORT,'append-only'); END;
CREATE TRIGGER revisions_no_delete BEFORE DELETE ON revisions BEGIN SELECT RAISE(ABORT,'append-only'); END;
CREATE TRIGGER reselections_no_update BEFORE UPDATE ON reselections BEGIN SELECT RAISE(ABORT,'append-only'); END;
CREATE TRIGGER reselections_no_delete BEFORE DELETE ON reselections BEGIN SELECT RAISE(ABORT,'append-only'); END;
CREATE TRIGGER profiles_no_update BEFORE UPDATE ON profiles BEGIN SELECT RAISE(ABORT,'append-only'); END;
CREATE TRIGGER profiles_no_delete BEFORE DELETE ON profiles BEGIN SELECT RAISE(ABORT,'append-only'); END;
CREATE TRIGGER distributions_no_update BEFORE UPDATE ON distributions BEGIN SELECT RAISE(ABORT,'append-only'); END;
CREATE TRIGGER distributions_no_delete BEFORE DELETE ON distributions BEGIN SELECT RAISE(ABORT,'append-only'); END;
CREATE TRIGGER planning_snapshots_no_update BEFORE UPDATE ON planning_snapshots BEGIN SELECT RAISE(ABORT,'append-only'); END;
CREATE TRIGGER planning_snapshots_no_delete BEFORE DELETE ON planning_snapshots BEGIN SELECT RAISE(ABORT,'append-only'); END;
CREATE TRIGGER planning_decisions_no_update BEFORE UPDATE ON planning_decisions BEGIN SELECT RAISE(ABORT,'append-only'); END;
CREATE TRIGGER planning_decisions_no_delete BEFORE DELETE ON planning_decisions BEGIN SELECT RAISE(ABORT,'append-only'); END;
CREATE TRIGGER transfer_receipts_no_update BEFORE UPDATE ON transfer_receipts BEGIN SELECT RAISE(ABORT,'append-only'); END;
CREATE TRIGGER transfer_receipts_no_delete BEFORE DELETE ON transfer_receipts BEGIN SELECT RAISE(ABORT,'append-only'); END;
PRAGMA user_version=3;
COMMIT;
