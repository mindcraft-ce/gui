using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using mindcraft_ce.Models;

namespace mindcraft_ce.ViewModels
{
    public class AgentViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<Agent> Agents { get; } = new();

        private Agent _selectedAgent;
        public Agent SelectedAgent
        {
            get => _selectedAgent;
            set
            {
                _selectedAgent = value;
                OnPropertyChanged();
            }
        }

        public AgentViewModel()
        {
            // TODO: Load all agents from the agents folder and set enabled values based on the settings file.
            Agents.Add(new Agent { Name = "Andy", Model = new Model("gpt-4o-mini") });
            Agents.Add(new Agent { Name = "Chloe", Model = new Model("gpt-3.5-turbo") });
            Agents.Add(new Agent { Name = "Zara", Model = new Model("gpt-4o") });
            Agents.Add(new Agent { Name = "Liam", Model = new Model("gpt-3.5") });
            Agents.Add(new Agent { Name = "Noah", Model = new Model("gpt-4") });
        }

        public void RemoveAgent(Agent agent)
        {
            if (Agents.Contains(agent))
            {
                Agents.Remove(agent);
                // TODO: Delete the agent's files from the agents folder.
                OnPropertyChanged(nameof(Agents));
            }
        }
        
        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }
}
