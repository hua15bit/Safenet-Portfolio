import { useEffect, useState } from 'react';
// Public SafeNet homepage: explains the project, missions, and available portals.
import AppLink from '../components/AppLink';
import logo from '../assets/images/Logo.png';
import useReveal from '../hooks/useReveal';
import '../styles/home.css';

const ANDROID_RELEASE_URL = '/downloads/v1.0.5/SafeNet-Release.apk';

const threats = [
  ['Phishing', 'Credential Theft', 'Identify suspicious emails, fake links, and fraudulent login prompts.', 'Verify senders and inspect links.'],
  ['Smishing', 'SMS Scam', 'Detect suspicious text messages, fake promos, and OTP traps.', 'Use official channels to verify messages.'],
  ['Social Engineering', 'Human Manipulation', 'Recognize pressure tactics, impersonation, and deceptive requests.', 'Pause and verify identity.'],
  ['Weak Passwords', 'Account Takeover', 'Choose stronger password practices during in-game choices.', 'Use strong, unique passwords.'],
  ['Unsafe Browsing', 'Malicious Web Activity', 'Avoid suspicious websites, pop-ups, and unsafe downloads.', 'Check site legitimacy first.'],
];
const protocols = [
  ['Campaign Engine', 'Organizes cybersecurity lessons into progressive missions.'],
  ['Scenario Core', 'Presents decisions where the player must choose safe online behavior.'],
  ['Scoring Matrix', 'Awards points based on decisions and mission completion.'],
  ['Feedback Processor', 'Explains the consequence of each player action.'],
  ['Leaderboard System', 'Encourages motivation, replayability, and healthy competition.'],
  ['Progress Tracker', 'Stores completed missions, achievements, and milestones.'],
];
const journey = ['Access Account', 'Select Campaign', 'Enter Cyberworld', 'Analyze Threat', 'Make Decision', 'Receive Feedback', 'Earn Score', 'Climb Rankings'];
const modules = [
  ['Professor Authentication Module', 'Secure professor registration, login, and logout.'],
  ['Professor Dashboard Module', 'Overview of classes, students, and performance.'],
  ['Class Management Module', 'Create and manage Living in the IT Era classes.'],
  ['Class Code Generation Module', 'Generate unique codes for student registration.'],
  ['Student Enrollment Monitoring Module', 'View students registered under each class.'],
  ['Points Monitoring Module', 'Review points and mission completion.'],
  ['Reports / Export Module', 'Create student activity summaries.'],
  ['Help / About Module', 'Provide portal instructions and system information.'],
];

function Heading({ mission, title, children }) {
  // Shared heading keeps the homepage sections visually consistent.
  return <div className="section-header reveal"><p className="eyebrow">{mission}</p><h2>{title}</h2>{children && <p>{children}</p>}</div>;
}

export default function HomePage() {
  // Manages homepage interactions such as navigation and visible sections.
  const [menuOpen, setMenuOpen] = useState(false);
  useReveal();
  useEffect(() => { document.title = 'SafeNet | Cyber Mission Interface'; }, []);
  const closeMenu = () => setMenuOpen(false);

  return (
    <div className="site-shell">
      <div className="background-system" /><div className="grid-overlay" /><div className="scanline" /><div className="noise" />
      <header className="topbar">
        <a className="brand" href="#home" onClick={closeMenu}><img src={logo} alt="SafeNet logo" /><span>SAFENET</span></a>
        <button className="nav-toggle" type="button" onClick={() => setMenuOpen((value) => !value)} aria-expanded={menuOpen}>☰</button>
        <nav className={`nav${menuOpen ? ' open' : ''}`}>
          <a href="#overview" onClick={closeMenu}>Briefing</a><a href="#threats" onClick={closeMenu}>Threat DB</a><a href="#protocols" onClick={closeMenu}>Protocols</a><a href="#flow" onClick={closeMenu}>Journey</a><a href="#modules" onClick={closeMenu}>Modules</a>
          <AppLink className="portal-nav-link" to="/student/register">Student Registration</AppLink><a href="#credits" onClick={closeMenu}>Credits</a>
        </nav>
      </header>
      <main>
        <section className="hero" id="home">
          <div className="hero-orbit hero-orbit-one" /><div className="hero-orbit hero-orbit-two" />
          <div className="status-rail reveal"><span>SYSTEM STATUS: <strong>ONLINE</strong></span><span>TRAINING MODE: <strong>ACTIVE</strong></span><span>THREAT SIMULATION: <strong>READY</strong></span></div>
          <div className="hero-content reveal">
            <img className="hero-logo" src={logo} alt="SafeNet logo" /><p className="eyebrow">CYBER MISSION INTERFACE</p><h1>SafeNet</h1><h2>Gamified 2D Cybersecurity Awareness</h2>
            <p className="hero-copy">Enter the cyberworld. Detect the threat. Defend your digital self through mission-based cybersecurity awareness training built for students.</p>
            <div className="hero-actions"><a className="asset-button download-button" href={ANDROID_RELEASE_URL} download="SafeNet-Release.apk"><span>DOWNLOAD GAME</span><small>ANDROID • V1.0.5 • 224.7 MB</small></a><a className="asset-button credits-button" href="#credits" aria-label="View credits" /></div>
          </div>
          <div className="hud-stats reveal"><div><strong>05</strong><span>Threat Types</span></div><div><strong>2D</strong><span>Cyberworld</span></div><div><strong>XP</strong><span>Scoring Core</span></div><div><strong>PH</strong><span>Localized Learning</span></div></div>
        </section>
        <section className="mission-section" id="overview">
          <Heading mission="MISSION 01" title="System Overview">SafeNet transforms passive instruction into an interactive student-centered simulation.</Heading>
          <div className="overview-grid"><article className="terminal-panel reveal"><span className="panel-label">MISSION BRIEFING</span><h3>What is SafeNet?</h3><p>A 2D gamified awareness system that helps students identify and respond to common cyber threats.</p><p>Players enter scenarios, make decisions, receive feedback, and progress through campaigns.</p></article><article className="terminal-panel reveal"><span className="panel-label">CORE OBJECTIVE</span><div className="core-objective"><span>Detect</span><span>Decide</span><span>Defend</span></div><p>Practice safe decisions through immediate feedback and mission progression.</p></article></div>
        </section>
        <section className="mission-section" id="threats">
          <Heading mission="MISSION 02" title="Threat Database">Enemy intel explains the risk, player task, and defense skill.</Heading>
          <div className="threat-grid">{threats.map(([name, type, task, defense], index) => <article className="threat-card reveal" key={name}><span className="threat-code">ENEMY INTEL {String(index + 1).padStart(2, '0')}</span><h3>{name}</h3><p><strong>Threat Type:</strong> {type}</p><p><strong>Player Task:</strong> {task}</p><p><strong>Defense Skill:</strong> {defense}</p></article>)}</div>
        </section>
        <section className="mission-section" id="protocols">
          <Heading mission="MISSION 03" title="Training Protocols">Each feature supports awareness, engagement, and measurable progress.</Heading>
          <div className="protocol-grid">{protocols.map(([title, text]) => <article className="upgrade-card reveal" key={title}><h3>{title}</h3><p>{text}</p></article>)}</div>
        </section>
        <section className="mission-section" id="flow">
          <Heading mission="MISSION 04" title="Player Journey">A game-like path from access to awareness mastery.</Heading>
          <div className="journey reveal">{journey.map((step, index) => <div key={step}><span>{String(index + 1).padStart(2, '0')}</span><p>{step}</p></div>)}</div>
        </section>
        <section className="mission-section" id="modules">
          <Heading mission="MISSION 05" title="System Modules">A professor portal combined with game-based learning and student monitoring.</Heading>
          <div className="module-grid module-intro-grid"><article className="terminal-panel reveal"><span className="panel-label">PROFESSOR WEB PORTAL</span><h3>Class Monitoring Command Center</h3><p>Create classes, generate registration codes, and monitor students.</p><AppLink className="portal-launch-button" to="/student/register">STUDENT REGISTRATION</AppLink></article><article className="terminal-panel reveal"><span className="panel-label">GAME SYSTEM</span><h3>Cyber Training Core</h3><ul className="feature-bullet-list"><li>Game Play Module</li><li>Campaign Module</li><li>Mission and Challenge Module</li><li>Scoring and Feedback Module</li><li>Progress and Achievements Module</li><li>Data Management and Sync Module</li></ul></article></div>
          <div className="professor-module-list">{modules.map(([title, text]) => <article className="professor-module-card reveal" key={title}><div><h3>{title}</h3><p>{text}</p></div></article>)}</div>
        </section>
        <section className="mission-section importance"><div className="terminal-panel wide reveal"><span className="panel-label">MISSION 06 // WHY IT MATTERS</span><h2>Digital confidence is not the same as cyber readiness.</h2><p>SafeNet turns cybersecurity education into active, memorable, scenario-based learning.</p></div></section>
        <section className="mission-section credits-section" id="credits">
          <Heading mission="CREDITS TERMINAL" title="Project Team" />
          <div className="credits-board reveal"><div className="credits-content"><img src={logo} alt="SafeNet logo" /><h3>SafeNet: Gamified 2D Cybersecurity Awareness</h3><p className="credit-label">Developed by</p><p>Bautista, Joshua Angelo S.<br />Lazaro, Calvin Kyle A.<br />Majait, Cristan Rush G.<br />Pidor, Marcus Vincent<br />Rumuar, Hero Jhon C.<br />Villacarlos, Jake Rei Inri G.</p><p className="credit-label">Capstone Adviser</p><p>John Clement Escobañez</p><p className="credit-label">Institution</p><p>National University<br />College of Computing and Information Technologies</p></div></div>
        </section>
      </main>
      <footer className="footer"><p>SafeNet Cyber Mission Interface • Capstone Project 2026</p><p>Enter the cyberworld. Detect the threat. Defend your digital self.</p><p><AppLink className="faculty-access-link" to="/professor">Faculty Access</AppLink></p></footer>
    </div>
  );
}
