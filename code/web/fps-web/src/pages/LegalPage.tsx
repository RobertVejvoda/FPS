import { Link } from 'react-router-dom';

const sourceUrl = 'https://github.com/RobertVejvoda/fairspot';
const licenseUrl = 'https://github.com/RobertVejvoda/fairspot/blob/master/LICENSE';
const brandPolicyUrl = 'https://github.com/RobertVejvoda/fairspot/blob/master/docs/strategy-layer/brand-policy.md';

export function LegalPage() {
  return (
    <div className="legal-page">
      <div className="legal-panel">
        <div className="brand-lockup">
          <div className="brand-mark" aria-hidden="true">F</div>
          <div className="brand-title">
            <strong>FairSpot</strong>
            <span>Legal notices</span>
          </div>
        </div>

        <h1>About FairSpot</h1>
        <p>
          FairSpot is open-source software for fair workplace resource allocation,
          auditability, and tenant-owned operation.
        </p>

        <dl className="legal-list">
          <div>
            <dt>Copyright</dt>
            <dd>Copyright (c) 2026 Robert Vejvoda.</dd>
          </div>
          <div>
            <dt>License</dt>
            <dd>
              FairSpot is licensed under the GNU Affero General Public License
              version 3 or later.
            </dd>
          </div>
          <div>
            <dt>Source code</dt>
            <dd>
              <a href={sourceUrl} target="_blank" rel="noreferrer">{sourceUrl}</a>
            </dd>
          </div>
          <div>
            <dt>Brand use</dt>
            <dd>
              The FairSpot name and logo identify Robert Vejvoda's project. Forks,
              hosted offers, and modified deployments must not imply endorsement
              or official FairSpot status unless separately agreed.
            </dd>
          </div>
        </dl>

        <div className="legal-actions">
          <a className="btn-secondary" href={licenseUrl} target="_blank" rel="noreferrer">License</a>
          <a className="btn-secondary" href={brandPolicyUrl} target="_blank" rel="noreferrer">Brand policy</a>
          <Link className="btn-primary" to="/session">Back to app</Link>
        </div>
      </div>
    </div>
  );
}
